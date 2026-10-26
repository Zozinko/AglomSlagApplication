import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { randomBytes, randomUUID, pbkdf2Sync } from 'node:crypto';
import { graphql, expectErrors } from '../api/helpers.mjs';

const project = process.env.API_TEST_COMPOSE_PROJECT;
const composeFile = process.env.API_TEST_COMPOSE_FILE;
assert.match(project ?? '', /^aglom-api-tests-[a-f0-9]{12}$/, 'Password tests require the isolated runner');
assert.ok(composeFile);
const prefix = ['compose', '-p', project, '-f', composeFile];
const sql = query => execFileSync('docker', [...prefix, 'exec', '-T', 'postgres', 'psql', '-X', '-A', '-t', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', 'Auth'], {
  input: query, encoding: 'utf8', timeout: 30_000,
}).trim();
const quote = value => `'${String(value).replaceAll("'", "''")}'`;
const secret = () => `${randomBytes(24).toString('base64url')}!aA9`;
const loginQuery = 'mutation($input:LoginInput!) { login(input:$input) { accessToken refreshToken user { userId userName userEmail admin } } }';
const login = ({ email, password, refreshToken }) => graphql(loginQuery, { input: { email, password, refreshToken } }, null);
const change = (token, currentPassword, newPassword) => graphql('mutation($currentPassword:String!,$newPassword:String!) { changePassword(currentPassword:$currentPassword,newPassword:$newPassword) }', { currentPassword, newPassword }, token);
const valid = response => {
  assert.equal(response.body.errors, undefined, 'Expected successful password/session operation');
  assert.ok(response.body.data?.login?.accessToken);
  return response.body.data.login;
};
const denied = response => assert.ok(response.body.errors?.some(error => error.extensions?.code === 'INVALID_CREDENTIALS'), 'Expected generic invalid credentials response');

// Values are inherited through -e NAME, never included in argv or printed on failure.
function operator(command, values) {
  const result = spawnSync('docker', [...prefix, 'run', '--rm', '--no-deps', ...Object.keys(values).flatMap(name => ['-e', name]), '--entrypoint', 'dotnet', 'api', 'AglomSlagServer.dll', command], {
    encoding: 'utf8', timeout: 120_000, maxBuffer: 1024 * 1024, env: { ...process.env, ...values },
  });
  if (result.error) throw new Error('Password operator process failed');
  return result;
}

function account(t, password = secret()) {
  const email = `password-${randomUUID()}@example.invalid`;
  const created = operator('--bootstrap-user', { BOOTSTRAP_EMAIL: email, BOOTSTRAP_PASSWORD: password });
  assert.equal(created.status, 0, 'Explicit bootstrap must succeed');
  const id = Number(sql(`SELECT "UserId" FROM "Users" WHERE "UserEmail"=${quote(email)};`));
  assert.ok(id > 0);
  t.after(() => sql(`DELETE FROM "RefreshSessions" WHERE "UserId"=${id}; DELETE FROM "Users" WHERE "UserId"=${id};`));
  return { email, password, id, hash: () => sql(`SELECT "PasswordHash" FROM "Users" WHERE "UserId"=${id};`) };
}

function envelope(hash) {
  const bytes = Buffer.from(hash, 'base64');
  assert.equal(bytes[0], 1, 'Identity V3 format marker');
  assert.equal(bytes.readUInt32BE(1), 2, 'PBKDF2 HMAC SHA512 PRF');
  assert.equal(bytes.readUInt32BE(5), 210_000, 'Configured iteration count');
  assert.ok(bytes.readUInt32BE(9) >= 16, 'At least 128 bit random salt');
  return bytes;
}

test('SEC-04: managed bootstrap salts identical passwords independently and never exposes credentials', async t => {
  const password = secret();
  const first = account(t, password);
  const second = account(t, password);
  const hashes = [first.hash(), second.hash()];
  hashes.forEach(envelope);
  assert.ok(hashes[0] !== hashes[1], 'Equal passwords require different salted hashes');
  assert.ok(hashes.every(hash => hash !== password && !hash.includes(password)), 'Database stores hashes only');
  for (const current of [first, second]) {
    const response = await login(current);
    valid(response);
    assert.ok(!JSON.stringify(response.body).includes(password), 'No plaintext in response');
    assert.ok(hashes.every(hash => !JSON.stringify(response.body).includes(hash)), 'No hash in response');
    denied(await login({ email: current.email, password: secret() }));
  }
  const duplicate = operator('--bootstrap-user', { BOOTSTRAP_EMAIL: first.email, BOOTSTRAP_PASSWORD: secret() });
  assert.notEqual(duplicate.status, 0, 'Bootstrap must refuse to overwrite an existing password');
  assert.ok(first.hash() === hashes[0]);
  for (const rejected of ['', 'short', ' '.repeat(12), 'x'.repeat(1025)]) {
    const result = operator('--bootstrap-user', { BOOTSTRAP_EMAIL: `rejected-${randomUUID()}@example.invalid`, BOOTSTRAP_PASSWORD: rejected });
    assert.notEqual(result.status, 0, 'Invalid passwords must not create an account');
  }
  const logs = execFileSync('docker', [...prefix, 'logs', '--no-color', 'api'], { encoding: 'utf8', timeout: 30_000 });
  assert.ok(!logs.includes(password), 'No plaintext in API logs');
  assert.ok(hashes.every(hash => !logs.includes(hash)), 'No hash in API logs');
});

test('SEC-04: self password change verifies current password and revokes every previous refresh session', async t => {
  const current = account(t);
  const first = valid(await login(current));
  const second = valid(await login(current));
  const before = current.hash();
  denied(await change(first.accessToken, secret(), secret()));
  assert.ok(current.hash() === before, 'Wrong current password must leave the hash unchanged');
  expectErrors(await change(first.accessToken, current.password, ''), { code: 'VALIDATION_FAILED' });
  assert.ok(current.hash() === before, 'Empty update must leave the hash unchanged');
  expectErrors(await change(null, current.password, secret()), { code: 'AUTH_NOT_AUTHENTICATED' });
  const replacement = secret();
  const changed = await change(first.accessToken, current.password, replacement);
  assert.equal(changed.body.errors, undefined);
  assert.equal(changed.body.data.changePassword, true);
  assert.ok(!JSON.stringify(changed.body).includes(replacement));
  denied(await login(current));
  valid(await login({ email: current.email, password: replacement }));
  denied(await login({ refreshToken: first.refreshToken }));
  denied(await login({ refreshToken: second.refreshToken }));
  assert.equal(sql(`SELECT COUNT(*) FROM "RefreshSessions" WHERE "UserId"=${current.id} AND "RevokedAt" IS NULL;`), '1');
  assert.ok(current.hash() !== before);
  envelope(current.hash());
});

test('SEC-04: operator reset and disable revoke sessions; malformed and legacy hashes fail safely', async t => {
  const current = account(t);
  const initial = valid(await login(current));
  const before = current.hash();
  const empty = operator('--reset-password', { TARGET_USER_ID: String(current.id), NEW_PASSWORD: '' });
  assert.notEqual(empty.status, 0);
  assert.ok(current.hash() === before, 'Empty reset must leave credential unchanged');
  const replacement = secret();
  assert.equal(operator('--reset-password', { TARGET_USER_ID: String(current.id), NEW_PASSWORD: replacement }).status, 0);
  denied(await login({ refreshToken: initial.refreshToken }));
  denied(await login(current));
  const active = valid(await login({ email: current.email, password: replacement }));
  assert.equal(operator('--disable-user', { TARGET_USER_ID: String(current.id) }).status, 0);
  denied(await login({ refreshToken: active.refreshToken }));
  denied(await login({ email: current.email, password: replacement }));
  assert.equal(sql(`SELECT "Disabled" FROM "Users" WHERE "UserId"=${current.id};`), 't');
  assert.equal(operator('--reset-password', { TARGET_USER_ID: String(current.id), NEW_PASSWORD: replacement }).status, 0);
  const activeAfterReset = valid(await login({ email: current.email, password: replacement }));
  for (const hash of ['Password', '!not-base64!', Buffer.alloc(12).toString('base64'), Buffer.alloc(64).toString('base64')]) {
    sql(`UPDATE "Users" SET "PasswordHash"=${quote(hash)} WHERE "UserId"=${current.id};`);
    denied(await login({ email: current.email, password: hash }));
  }
  denied(await login({ refreshToken: activeAfterReset.refreshToken }));
  denied(await login({ email: `absent-${randomUUID()}@example.invalid`, password: replacement }));
  denied(await login({ email: 'test@test.test', password: 'Password' }));
});

test('SEC-04: a successful lower-cost Identity V3 credential upgrades parameters and invalidates old sessions', async t => {
  const current = account(t);
  const olderSession = valid(await login(current));
  // Independent fixture using the documented Identity V3 envelope: marker, PRF,
  // iteration count, salt length, salt, derived subkey (all integers big-endian).
  const salt = randomBytes(16);
  const header = Buffer.alloc(13);
  header[0] = 1;
  header.writeUInt32BE(2, 1);
  header.writeUInt32BE(10_000, 5);
  header.writeUInt32BE(salt.length, 9);
  const weaker = Buffer.concat([header, salt, pbkdf2Sync(current.password, salt, 10_000, 32, 'sha512')]).toString('base64');
  // Simulate a session issued before the configured KDF cost was increased. Resetting
  // the fixture's revocation is SQL setup only, never a supported application action.
  sql(`UPDATE "Users" SET "PasswordHash"=${quote(weaker)} WHERE "UserId"=${current.id}; UPDATE "RefreshSessions" SET "RevokedAt"=NULL WHERE "UserId"=${current.id};`);
  denied(await login({ email: current.email, password: secret() }));
  assert.ok(current.hash() === weaker, 'Failed login must not upgrade credentials');
  const loggedIn = valid(await login(current));
  const upgraded = current.hash();
  envelope(upgraded);
  assert.ok(upgraded !== weaker, 'Successful login replaces weak credential');
  denied(await login({ refreshToken: olderSession.refreshToken }));
  valid(await login({ refreshToken: loggedIn.refreshToken }));
});
