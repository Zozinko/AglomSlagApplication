import test from 'node:test';
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import { graphql, expectErrors } from './helpers.mjs';

const loginDocument = `mutation($input:LoginInput!) { login(input:$input) {
  accessToken accessExpiresAt refreshToken refreshExpiresAt user { userId userEmail admin }
} }`;
const login = input => graphql(loginDocument, { input }, null);
const refresh = refreshToken => login({ refreshToken });
const credentials = () => ({ email: process.env.API_TEST_ADMIN_EMAIL, password: process.env.API_TEST_ADMIN_PASSWORD });
function payload(response) {
  assert.equal(response.body.errors, undefined, JSON.stringify(response.body.errors));
  return response.body.data.login;
}
function denied(response) { expectErrors(response, { field: 'login', code: 'INVALID_CREDENTIALS' }); }
async function logout(session) {
  const result = await graphql('mutation { logout }', {}, session.accessToken);
  assert.equal(result.body.errors, undefined, JSON.stringify(result.body.errors));
  assert.equal(result.body.data.logout, true);
}

test('SEC-03: Login returns rotating JSON token pair, new access identity and bounded expirations', async () => {
  const first = payload(await login(credentials()));
  assert.match(first.refreshToken, /^[A-Za-z0-9_-]{43}$/);
  assert.ok(Date.parse(first.accessExpiresAt) > Date.now());
  assert.ok(Date.parse(first.refreshExpiresAt) > Date.parse(first.accessExpiresAt));
  const next = payload(await refresh(first.refreshToken));
  assert.notEqual(next.refreshToken, first.refreshToken);
  assert.notEqual(next.accessToken, first.accessToken);
  assert.deepEqual(next.user, first.user);
  const claims = token => JSON.parse(Buffer.from(token.split('.')[1], 'base64url'));
  assert.equal(claims(first.accessToken).sid, claims(next.accessToken).sid);
  assert.equal(claims(next.accessToken).sub, String(first.user.userId));
  const hello = await graphql('{ hello }', {}, next.accessToken);
  assert.equal(hello.body.data.hello, 'Aglom GraphQL API');
  await logout(next);
});

test('SEC-03: refresh works with expired access JWT, and credentials/refresh modes are exclusive', async () => {
  const first = payload(await login(credentials()));
  const claims = JSON.parse(Buffer.from(first.accessToken.split('.')[1], 'base64url'));
  const now = Math.floor(Date.now() / 1000);
  const header = Buffer.from(JSON.stringify({ alg: 'HS256', typ: 'JWT' })).toString('base64url');
  const body = Buffer.from(JSON.stringify({ ...claims, nbf: now - 300, exp: now - 120 })).toString('base64url');
  const unsigned = `${header}.${body}`;
  const expired = `${unsigned}.${createHmac('sha256', process.env.API_TEST_JWT_KEY).update(unsigned).digest('base64url')}`;
  expectErrors(await graphql('{ hello }', {}, expired), { code: 'AUTH_NOT_AUTHENTICATED' });
  const next = payload(await graphql(loginDocument, { input: { refreshToken: first.refreshToken } }, expired));
  for (const input of [{}, { refreshToken: '' }, { refreshToken: 'x'.repeat(43) },
    { refreshToken: next.refreshToken, email: credentials().email },
    { refreshToken: next.refreshToken, password: credentials().password },
    { ...credentials(), refreshToken: next.refreshToken }]) denied(await login(input));
  // Rejected mixed modes do not consume the legitimate refresh.
  await logout(payload(await refresh(next.refreshToken)));
});

test('SEC-03: replay revokes the whole family, including descendants, but not another login', async () => {
  const first = payload(await login(credentials()));
  const independent = payload(await login(credentials()));
  const next = payload(await refresh(first.refreshToken));
  const descendant = payload(await refresh(next.refreshToken));
  denied(await refresh(first.refreshToken));
  denied(await refresh(descendant.refreshToken));
  denied(await refresh(next.refreshToken));
  const unaffected = payload(await refresh(independent.refreshToken));
  // Ordinary signed access is intentionally valid until exp after revocation.
  assert.equal((await graphql('{ hello }', {}, descendant.accessToken)).body.data.hello, 'Aglom GraphQL API');
  await logout(unaffected);
});

test('SEC-03: concurrent use produces one rotation and replay revokes its replacement', async () => {
  const first = payload(await login(credentials()));
  const results = await Promise.all([refresh(first.refreshToken), refresh(first.refreshToken)]);
  const accepted = results.filter(r => !r.body.errors);
  const rejected = results.filter(r => r.body.errors);
  assert.equal(accepted.length, 1);
  assert.equal(rejected.length, 1);
  denied(rejected[0]);
  denied(await refresh(payload(accepted[0]).refreshToken));
});

test('SEC-03: lost rotation response requires password login; Logout is protected and family-scoped', async () => {
  expectErrors(await graphql('mutation { logout }', {}, null), { code: 'AUTH_NOT_AUTHENTICATED' });
  const first = payload(await login(credentials()));
  payload(await refresh(first.refreshToken)); // Simulate losing the successful response.
  denied(await refresh(first.refreshToken));
  const replacementLogin = payload(await login(credentials()));
  const independent = payload(await login(credentials()));
  const rotated = payload(await refresh(replacementLogin.refreshToken));
  await logout(replacementLogin); // An earlier access in this family revokes latest refresh too.
  await logout(replacementLogin); // Idempotent.
  denied(await refresh(rotated.refreshToken));
  await logout(payload(await refresh(independent.refreshToken)));
});
