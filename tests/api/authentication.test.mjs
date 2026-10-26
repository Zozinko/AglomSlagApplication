import test from 'node:test';
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import { document, example, operation, graphql, fixture } from './helpers.mjs';

const endpoint = () => process.env.GRAPHQL_TEST_URL;
export async function rawGraphql(query, variables = {}, token, extra = {}) {
  const response = await fetch(endpoint(), { method: 'POST', headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) }, body: JSON.stringify({ query, variables, ...extra }) });
  return { status: response.status, body: await response.json() };
}
const credentials = () => ({ email: process.env.API_TEST_ADMIN_EMAIL, password: process.env.API_TEST_ADMIN_PASSWORD });
const rejected = r => {
  assert.ok(r.body.errors?.some(e => e.extensions?.code === 'AUTH_NOT_AUTHENTICATED'), JSON.stringify(r.body));
  assert.ok(!r.body.data || Object.values(r.body.data).every(v => v === null));
};
function signed(overrides = {}, key = process.env.API_TEST_JWT_KEY, algorithm = 'HS256') {
  const b64 = v => Buffer.from(JSON.stringify(v)).toString('base64url');
  const now = Math.floor(Date.now() / 1000);
  const data = `${b64({ alg: algorithm, typ: 'JWT' })}.${b64({ sub: '-2', admin: 'true', iss: 'aglom-tests', aud: 'aglom-test-clients', nbf: now - 10, exp: now + 300, ...overrides })}`;
  return `${data}.${createHmac(algorithm === 'HS384' ? 'sha384' : 'sha256', key).update(data).digest('base64url')}`;
}

test('SEC-02: credentials login issues a short lived JWT and a safe user DTO', async () => {
  const r = await rawGraphql(document('Login'), { input: credentials() });
  assert.equal(r.body.errors, undefined, JSON.stringify(r.body));
  const payload = r.body.data.login;
  assert.equal(payload.user.userId, -2);
  assert.equal(payload.user.admin, true);
  assert.ok(Date.parse(payload.accessExpiresAt) > Date.now());
  assert.ok(Date.parse(payload.accessExpiresAt) <= Date.now() + 901_000);
  const claims = JSON.parse(Buffer.from(payload.accessToken.split('.')[1], 'base64url'));
  assert.equal(claims.sub, '-2');
  assert.equal(claims.iss, 'aglom-tests');
  assert.equal(claims.aud, 'aglom-test-clients');
  assert.doesNotMatch(JSON.stringify(claims), /password|refresh|hash/i);
  assert.equal((await rawGraphql('{ hello }', {}, payload.accessToken)).body.data.hello, 'Aglom GraphQL API');
});

test('SEC-02: unknown email and incorrect password have the same public response', async () => {
  const responses = [];
  for (const input of [{ ...credentials(), email: 'absent@example.invalid' }, { ...credentials(), password: 'incorrect-password' }, { email: 'test@test.test', password: 'Password' }]) {
    const r = await rawGraphql(document('Login'), { input });
    assert.equal(r.body.errors?.[0]?.extensions?.code, 'INVALID_CREDENTIALS');
    responses.push(r.body.errors[0].message);
    assert.doesNotMatch(JSON.stringify(r.body), /PasswordHash|stackTrace|Npgsql|SELECT /);
  }
  assert.equal(new Set(responses).size, 1);
});

test('SEC-02: all sixteen existing operations refuse anonymous execution', async () => {
  for (const name of ['Hello', 'BlastFurnaces', 'ComponentGuides', 'CalcVariants', 'CalcVariantById', 'CalculateAglom', 'CalculateSlagMode', 'AddBlastFurnace', 'UpdateBlastFurnace', 'DeleteBlastFurnace', 'AddComponentGuide', 'UpdateComponentGuide', 'DeleteComponentGuide', 'AddCalcVariant', 'UpdateCalcVariant', 'DeleteCalcVariant']) {
    rejected(await rawGraphql(document(name), example(name)));
  }
});

test('SEC-02: expired, forged, wrong issuer/audience and disallowed algorithm tokens fail', async () => {
  const now = Math.floor(Date.now() / 1000);
  for (const token of [signed({ exp: now - 120 }), signed({ iss: 'other' }), signed({ aud: 'other' }), signed({}, 'x'.repeat(64)), signed({}, process.env.API_TEST_JWT_KEY, 'HS384'), 'not-a-token'])
    rejected(await rawGraphql('{ hello }', {}, token));
});

test('SEC-02: mixed login, aliases, fragments and operation selection cannot run an unauthorized write', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const r = await rawGraphql(`mutation($input:LoginInput!,$id:Int!) { login(input:$input) { accessToken } alias:addBlastFurnace(bfName:"unauthorized-created") { bfId } updateBlastFurnace(bfId:$id,bfName:"unauthorized-change") { bfId } }`, { input: credentials(), id: furnace.bfId });
  assert.ok(r.body.errors?.some(e => e.extensions?.code === 'AUTH_NOT_AUTHENTICATED'));
  const after = (await operation('BlastFurnaces')).blastFurnaces;
  assert.equal(after.find(x => x.bfId === furnace.bfId).bfName, furnace.bfName);
  assert.ok(!after.some(x => x.bfName === 'unauthorized-created'));
  rejected(await rawGraphql('query { ...Hidden } fragment Hidden on Query { alias:hello }'));
  rejected(await rawGraphql('query Open { __typename } query Hidden { hello }', {}, undefined, { operationName: 'Hidden' }));
});

test('HTTP GraphQL UI and schema are public; GET business queries still require JWT', async () => {
  const ui = await fetch(endpoint(), { headers: { accept: 'text/html' } });
  assert.equal(ui.status, 200);
  assert.match(ui.headers.get('content-type'), /text\/html/);
  assert.ok((await ui.text()).length > 100);
  const root = await fetch(new URL('/', endpoint()), { headers: { accept: 'text/html' } });
  assert.equal(root.status, 200);
  assert.match(new URL(root.url).pathname, /^\/graphql\/?$/);
  const schema = await fetch(endpoint() + '?sdl');
  assert.equal(schema.status, 200);
  const sdl = await schema.text();
  assert.match(sdl, /type Query/);
  assert.doesNotMatch(sdl, /passwordHash/);
  const introspection = await rawGraphql('{ __schema { queryType { name } } }');
  assert.equal(introspection.body.errors, undefined);
  assert.equal(introspection.body.data.__schema.queryType.name, 'Query');
  const query = endpoint() + '?query=' + encodeURIComponent('{ hello }');
  const anonymous = await fetch(query, { headers: { accept: 'application/json' } });
  rejected({ status: anonymous.status, body: await anonymous.json() });
  const authenticated = await fetch(query, { headers: { accept: 'application/json', authorization: `Bearer ${process.env.API_TEST_ACCESS_TOKEN}` } });
  const body = await authenticated.json();
  assert.equal(body.errors, undefined);
  assert.equal(body.data.hello, 'Aglom GraphQL API');
});
