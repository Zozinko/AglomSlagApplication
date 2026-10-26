import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';

const project = process.env.API_TEST_COMPOSE_PROJECT;
assert.match(project ?? '', /^aglom-api-tests-[a-f0-9]{12}$/);
const prefix = ['compose', '-p', project, '-f', process.env.API_TEST_COMPOSE_FILE];
const docker = (...args) => execFileSync('docker', [...prefix, ...args], { encoding: 'utf8', timeout: 90_000 });

test('SEC-02: missing or invalid JWT settings fail startup without revealing key material', () => {
  for (const [key, value] of [['JWT_SIGNING_KEY',''], ['JWT_SIGNING_KEY','too-short-private-key'], ['JWT_ISSUER',''], ['JWT_AUDIENCE',''], ['JWT_ACCESS_SECONDS','901'], ['JWT_CLOCK_SKEW_SECONDS','-1']]) {
    const result = spawnSync('docker', [...prefix, 'run', '--rm', '--no-deps', '-e', `${key}=${value}`, 'api'], { encoding: 'utf8', timeout: 60_000 });
    assert.equal(result.error, undefined);
    assert.notEqual(result.status, 0);
    const output = result.stdout + result.stderr;
    assert.ok(!output.includes(process.env.API_TEST_JWT_KEY));
    assert.ok(!output.includes('too-short-private-key'));
    assert.match(output, /Configuration error/);
    assert.doesNotMatch(output, /Now listening/);
  }
});

test('Startup creates demo accounts automatically and preserves their hashes on restart', async () => {
  const sql = query => execFileSync('docker', [...prefix, 'exec', '-T', 'postgres', 'psql', '-X', '-A', '-t', '-U', 'postgres', '-d', 'Auth', '-c', query], { encoding: 'utf8' }).trim();
  const snapshot = () => sql('SELECT "UserId"::text || \'|\' || "Admin"::text || \'|\' || "PasswordHash" FROM "Users" WHERE "UserId" IN (-1,-2) ORDER BY "UserId";');
  const before = snapshot();
  assert.equal(before.split('\n').length, 2);
  docker('restart', 'api');
  const address = docker('port', 'api', '8080').trim();
  process.env.GRAPHQL_TEST_URL = `http://${address}/graphql`;
  let ready = false;
  for (let i = 0; i < 100; i++) {
    try {
      const response = await fetch(process.env.GRAPHQL_TEST_URL + '?sdl');
      if (response.ok && (await response.text()).includes('type Query')) { ready = true; break; }
    } catch { }
    await delay(100);
  }
  assert.ok(ready);
  assert.ok(snapshot() === before, 'Normal startup must not reset passwords or create duplicates');
});

test('SEC-02/04: rate limit counts individual Login aliases and password verification attempts', async () => {
  const name = `${project}-login-limit`;
  try {
    docker('run', '-d', '--no-deps', '--name', name, '-p', '127.0.0.1::8080', '-e', 'AUTH_LOGIN_LIMIT=2', 'api');
    const published = execFileSync('docker', ['port', name, '8080/tcp'], { encoding: 'utf8' }).trim();
    assert.match(published, /^127\.0\.0\.1:\d+$/);
    const endpoint = `http://${published}/graphql`;
    let ready = false;
    for (let i = 0; i < 100; i++) {
      try {
        const r = await fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json', authorization: `Bearer ${process.env.API_TEST_ACCESS_TOKEN}` }, body: '{"query":"{hello}"}' });
        if ((await r.json()).data?.hello === 'Aglom GraphQL API') { ready = true; break; }
      } catch { }
      await delay(100);
    }
    assert.ok(ready, 'Rate-limit probe must start');
    const limited = await fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({
      query: 'mutation($input:LoginInput!){first:login(input:$input){accessToken} second:login(input:$input){accessToken} third:login(input:$input){accessToken}}',
      variables: { input: { email: process.env.API_TEST_ADMIN_EMAIL, password: process.env.API_TEST_ADMIN_PASSWORD } },
    }) });
    const limitedBody = await limited.json();
    assert.equal(limitedBody.errors?.[0]?.extensions?.code, 'LOGIN_RATE_LIMITED');
    assert.deepEqual(limitedBody.errors?.[0]?.path, ['third']);
    assert.equal(limited.headers.get('cache-control'), 'no-store');
    const r = await fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json', authorization: `Bearer ${process.env.API_TEST_ACCESS_TOKEN}` }, body: JSON.stringify({ query: 'mutation { changePassword(currentPassword:"incorrect-password",newPassword:"long-replacement-password") }' }) });
    const body = await r.json();
    assert.equal(body.errors?.[0]?.extensions?.code, 'LOGIN_RATE_LIMITED');
  } finally { execFileSync('docker', ['rm', '-f', name], { encoding: 'utf8' }); }
});
