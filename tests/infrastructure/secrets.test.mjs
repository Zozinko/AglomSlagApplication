import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const project = process.env.API_TEST_COMPOSE_PROJECT;
assert.match(project ?? '', /^aglom-api-tests-[a-f0-9]{12}$/);
const prefix = ['compose', '-p', project, '-f', process.env.API_TEST_COMPOSE_FILE];
const password = process.env.API_TEST_DB_PASSWORD;
assert.ok(password);
const docker = (...args) => execFileSync('docker', [...prefix, ...args], { encoding: 'utf8', timeout: 180_000 });
const direct = (...args) => execFileSync('docker', args, { encoding: 'utf8', timeout: 60_000 });
const safe = text => assert.ok(!text.includes(password), 'Output must not contain the test credential');
const connection = (database, credential) => `Host=postgres;Database=${database};Username=postgres;Password="${credential.replaceAll('"', '""')}"`;

test('SEC-01: missing password fails before listening and does not reveal values', () => {
  for (const service of ['api', 'migrator']) {
    const result = spawnSync('docker', [...prefix, 'run', '--rm', '--no-deps', '-e', 'DB_PASSWORD=', service], { encoding: 'utf8', timeout: 90_000 });
    assert.equal(result.error, undefined);
    assert.notEqual(result.status, 0);
    const logs = result.stdout + result.stderr;
    safe(logs);
    assert.match(logs, /required variable DB_PASSWORD is missing or empty/);
    assert.doesNotMatch(logs, /Now listening/);
  }
  const result = spawnSync('docker', ['compose', '--env-file', 'Soft/.env.example', '-f', 'Soft/Docker-compose.yml', 'config', '--quiet'], {
    encoding: 'utf8', env: { ...process.env, DB_PASSWORD: '' }, timeout: 30_000,
  });
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /DB_PASSWORD/);
  safe(result.stdout + result.stderr);
});

test('SEC-01: Git ignores working env files and retains only explicit templates', () => {
  for (const path of ['.env', 'Soft/.env', 'Soft/AglomSlagServer/.env.production', 'Soft/local.env']) {
    assert.equal(spawnSync('git', ['check-ignore', '-q', path]).status, 0, path);
  }
  for (const path of ['Soft/.env.example']) {
    assert.equal(spawnSync('git', ['check-ignore', '-q', '--no-index', path]).status, 1, path);
  }
});

test('SEC-01: API/migrator build contexts and image metadata contain no local env or credential', () => {
  for (const service of ['api', 'migrator']) {
    const image = docker('images', '-q', service).trim();
    assert.ok(image);
    safe(direct('image', 'inspect', image));
    safe(direct('history', '--no-trunc', image));
    const root = service === 'api' ? '/app' : '/src';
    const files = docker('run', '--rm', '--no-deps', '--entrypoint', 'find', service, root, '-type', 'f', '(', '-name', '.env', '-o', '-name', '.env.*', '-o', '-name', '*.env', ')');
    assert.equal(files.trim(), '', 'Even nested working env files and templates must stay outside the image');
  }
});

test('SEC-01: native host runtime loads Development env file and preserves process overrides', async () => {
  // Host SDK is not needed: copy the published framework-dependent API from the Linux image.
  // Only database/hello are exercised here; mathematical native dependencies stay in Docker.
  const work = mkdtempSync(join(tmpdir(), 'aglom-sec01-'));
  const container = `${project}-host-files`;
  let child;
  let logs = '';
  try {
    const image = docker('images', '-q', 'api').trim();
    direct('create', '--name', container, image);
    direct('cp', `${container}:/app/.`, work);
    const contentRoot = join(work, 'Soft', 'AglomSlagServer');
    mkdirSync(contentRoot, { recursive: true });
    const port = docker('port', 'postgres', '5432').trim().split(':').at(-1);
    const envFile = join(work, 'Soft', '.env');
    writeFileSync(envFile, `DB_HOST='localhost'\nDB_PORT='${port}'\nDB_USER='postgres'\nDB_PASSWORD='${password}'\n`);
    const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', JWT_SIGNING_KEY: process.env.API_TEST_JWT_KEY, JWT_ISSUER: 'aglom-tests', JWT_AUDIENCE: 'aglom-test-clients' };
    for (const key of Object.keys(env)) {
      if (/^(DB_|CONNECTION_|ConnectionStrings__|AGLOMSLAG_ENV_FILE)/i.test(key)) delete env[key];
    }
    // A wrong file host is overridden by process environment, while password stays literal.
    writeFileSync(envFile, `DB_HOST='invalid.invalid'\nDB_PORT='${port}'\nDB_USER='postgres'\nDB_PASSWORD='${password}'\n`);
    env.DB_HOST = 'localhost';
    child = spawn(process.env.API_TEST_DOTNET ?? 'dotnet', [join(work, 'AglomSlagServer.dll'), '--contentRoot', contentRoot, '--urls', 'http://127.0.0.1:0'], { env });
    child.stdout.on('data', data => { logs += data; });
    child.stderr.on('data', data => { logs += data; });
    let endpoint;
    for (let i = 0; i < 150; i++) {
      const match = logs.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
      if (match) { endpoint = `${match[1]}/graphql`; break; }
      if (child.exitCode !== null) break;
      await delay(100);
    }
    safe(logs);
    assert.ok(endpoint, `Native .NET 10 runtime must start using the same loader as Rider profiles: ${logs}`);
    assert.match(logs, /Database check succeeded for SlagModeContext/);
    assert.match(logs, /Database check succeeded for AuthContext/);
    const response = await fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json', authorization: `Bearer ${process.env.API_TEST_ACCESS_TOKEN}` }, body: '{"query":"{ hello blastFurnaces { bfId } }"}' });
    const body = await response.json();
    assert.equal(body.errors, undefined);
    assert.equal(body.data.hello, 'Aglom GraphQL API');
    assert.ok(body.data.blastFurnaces.length > 0);
  } finally {
    if (child && child.exitCode === null) {
      child.kill();
      await new Promise(resolve => child.once('exit', resolve));
    }
    direct('rm', '-f', container);
    rmSync(work, { recursive: true, force: true });
  }
});

test('SEC-01: rotating the existing role preserves data and both consumers use the new password', async () => {
  const rotated = `rotated-${password}`;
  const sql = text => execFileSync('docker', [...prefix, 'exec', '-T', 'postgres', 'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', 'postgres'], { input: text, encoding: 'utf8' });
  const alter = credential => sql(`ALTER ROLE postgres PASSWORD '${credential.replaceAll("'", "''")}';`);
  const countRows = () => docker('exec', '-T', 'postgres', 'psql', '-X', '-A', '-t', '-U', 'postgres', '-d', 'SlagMode', '-c', 'SELECT COUNT(*) FROM "BlastFurnaces";').trim();
  const before = countRows();
  try {
    alter(rotated);
    // The old API env still authenticates with the old password in new pooled connections.
    const old = spawnSync('docker', [...prefix, 'run', '--rm', '--no-deps', 'api'], { encoding: 'utf8', timeout: 45_000 });
    assert.notEqual(old.status, 0);
    safe(old.stdout + old.stderr);
    safe(docker('run', '--rm', '--no-deps', '-e', `DB_PASSWORD=${rotated}`, 'migrator'));
    // Both historical migrator aliases still override DB_* and apply both contexts.
    safe(docker('run', '--rm', '--no-deps', '-e', 'DB_PASSWORD=invalid-override',
      '-e', `CONNECTION_SLAG=${connection('SlagMode', rotated)}`,
      '-e', `CONNECTION_AUTH=${connection('Auth', rotated)}`, 'migrator'));
    const name = `${project}-rotation-check`;
    try {
      docker('run', '-d', '--no-deps', '--name', name, '-e', 'DB_PASSWORD=invalid-override',
        '-e', `ConnectionStrings__SlagModeContext=${connection('SlagMode', rotated)}`,
        '-e', `CONNECTION_AUTH=${connection('Auth', rotated)}`, 'api');
      let logs = '';
      for (let i = 0; i < 100; i++) {
        logs = direct('logs', name);
        if (/Now listening/.test(logs)) break;
        await delay(200);
      }
      safe(logs);
      assert.match(logs, /Now listening/);
      assert.match(logs, /Database check succeeded for SlagModeContext/);
      assert.match(logs, /Database check succeeded for AuthContext/);
      assert.equal(countRows(), before, 'Password rotation and repeated migrations preserve existing rows');
    } finally { direct('rm', '-f', name); }
  } finally { alter(password); }
});
