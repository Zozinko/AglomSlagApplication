import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';
import { graphql, expectErrors } from '../api/helpers.mjs';

const project = process.env.API_TEST_COMPOSE_PROJECT;
assert.match(project ?? '', /^aglom-api-tests-[a-f0-9]{12}$/);
assert.ok(process.env.API_TEST_COMPOSE_FILE);
const prefix = ['compose', '-p', project, '-f', process.env.API_TEST_COMPOSE_FILE];
const docker = (...args) => execFileSync('docker', [...prefix, ...args], { encoding: 'utf8', timeout: 180_000 });
const sql = query => execFileSync('docker', [...prefix, 'exec', '-T', 'postgres', 'psql', '-X', '-A', '-t', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', 'Auth'], {
  input: query, encoding: 'utf8', timeout: 30_000,
}).trim();
const loginDocument = `mutation($input:LoginInput!) { login(input:$input) { accessToken refreshToken user { userId admin } } }`;
const login = input => graphql(loginDocument, { input }, null);
const refresh = refreshToken => login({ refreshToken });
const hash = token => createHash('sha256').update(token, 'ascii').digest('hex').toUpperCase();
const hashWhere = token => `"TokenHash"='${hash(token)}'`;
function payload(response) {
  assert.equal(response.body.errors, undefined, JSON.stringify(response.body.errors));
  return response.body.data.login;
}
function denied(response) { expectErrors(response, { code: 'INVALID_CREDENTIALS', field: 'login' }); }
function userFixture(t) {
  const email = `session-${randomUUID()}@example.invalid`;
  const id = Number(sql(`INSERT INTO "Users" ("UserEmail","UserName","PasswordHash","Admin","Disabled")
    SELECT '${email}','Session test',"PasswordHash",true,false FROM "Users" WHERE "UserId"=-2 RETURNING "UserId";`).split('\n')[0]);
  assert.ok(Number.isInteger(id) && id > 0);
  t.after(() => sql(`UPDATE "RefreshSessions" SET "ReplacedById"=NULL WHERE "UserId"=${id};
    DELETE FROM "RefreshSessions" WHERE "UserId"=${id}; DELETE FROM "Users" WHERE "UserId"=${id};`));
  return { id, login: async () => payload(await login({ email, password: process.env.API_TEST_ADMIN_PASSWORD })) };
}

test('SEC-03: PostgreSQL stores hashes only, rotation links replacements and timestamps are UTC', async t => {
  const user = userFixture(t);
  const first = await user.login();
  const stored = JSON.parse(sql(`SELECT row_to_json(s) FROM "RefreshSessions" s WHERE ${hashWhere(first.refreshToken)};`));
  assert.equal(stored.TokenHash, hash(first.refreshToken));
  assert.equal(stored.UserId, user.id);
  assert.equal(stored.RevokedAt, null);
  assert.equal(stored.ReplacedById, null);
  assert.ok(Date.parse(stored.ExpiresAt) > Date.parse(stored.CreatedAt));
  assert.ok(!JSON.stringify(stored).includes(first.refreshToken));
  const next = payload(await refresh(first.refreshToken));
  assert.equal(sql(`SELECT count(*) FROM "RefreshSessions" old JOIN "RefreshSessions" replacement ON old."ReplacedById"=replacement."Id"
    WHERE old.${hashWhere(first.refreshToken)} AND replacement.${hashWhere(next.refreshToken)}
    AND old."RevokedAt" IS NOT NULL AND replacement."RevokedAt" IS NULL
    AND old."FamilyId"=replacement."FamilyId" AND old."UserId"=replacement."UserId";`), '1');
});

test('SEC-03: expired refresh is rejected and no replacement is inserted', async t => {
  const user = userFixture(t);
  const first = await user.login();
  sql(`UPDATE "RefreshSessions" SET "ExpiresAt"=clock_timestamp()-interval '1 second' WHERE ${hashWhere(first.refreshToken)};`);
  denied(await refresh(first.refreshToken));
  assert.equal(sql(`SELECT count(*) FROM "RefreshSessions" WHERE "UserId"=${user.id};`), '1');
  assert.equal(sql(`SELECT "RevokedAt" IS NOT NULL FROM "RefreshSessions" WHERE ${hashWhere(first.refreshToken)};`), 't');
});

test('SEC-03: DB credential changes and disable/re-enable revoke every family before another refresh', async t => {
  const user = userFixture(t);
  const first = await user.login();
  const independent = await user.login();
  sql(`UPDATE "Users" SET "Disabled"=true WHERE "UserId"=${user.id}; UPDATE "Users" SET "Disabled"=false WHERE "UserId"=${user.id};`);
  assert.equal(sql(`SELECT count(*) FROM "RefreshSessions" WHERE "UserId"=${user.id} AND "RevokedAt" IS NULL;`), '0');
  denied(await refresh(first.refreshToken));
  denied(await refresh(independent.refreshToken));
  const afterReenable = await user.login();
  sql(`UPDATE "Users" SET "PasswordHash"='invalidated-by-maintenance' WHERE "UserId"=${user.id};`);
  assert.equal(sql(`SELECT count(*) FROM "RefreshSessions" WHERE "UserId"=${user.id} AND "RevokedAt" IS NULL;`), '0');
  denied(await refresh(afterReenable.refreshToken));
  // Access has the explicitly documented exp-based lifetime.
  assert.equal((await graphql('{ hello }', {}, afterReenable.accessToken)).body.data.hello, 'Aglom GraphQL API');
});

test('SEC-03: unchanged credentials preserve refresh; refreshed privileges come from Auth DB', async t => {
  const user = userFixture(t);
  const first = await user.login();
  assert.equal(first.user.admin, true);
  sql(`UPDATE "Users" SET "PasswordHash"="PasswordHash", "Disabled"=false,"Admin"=false WHERE "UserId"=${user.id};`);
  const next = payload(await refresh(first.refreshToken));
  assert.equal(next.user.admin, false);
  assert.equal(JSON.parse(Buffer.from(next.accessToken.split('.')[1], 'base64url')).admin, 'false');
  assert.equal(JSON.parse(Buffer.from(first.accessToken.split('.')[1], 'base64url')).admin, 'true');
});

test('SEC-03: failure inserting replacement rolls back old-token consumption', async t => {
  const user = userFixture(t);
  const first = await user.login();
  sql(`CREATE FUNCTION api_test_reject_session() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN RAISE EXCEPTION 'forced session insertion failure' USING ERRCODE='23514'; END $$;
    CREATE TRIGGER api_test_session_failure BEFORE INSERT ON "RefreshSessions"
    FOR EACH ROW WHEN (NEW."UserId"=${user.id}) EXECUTE FUNCTION api_test_reject_session();`);
  try {
    expectErrors(await refresh(first.refreshToken));
    assert.equal(sql(`SELECT "RevokedAt" IS NULL AND "ReplacedById" IS NULL FROM "RefreshSessions" WHERE ${hashWhere(first.refreshToken)};`), 't');
    assert.equal(sql(`SELECT count(*) FROM "RefreshSessions" WHERE "UserId"=${user.id};`), '1');
  } finally {
    sql('DROP TRIGGER IF EXISTS api_test_session_failure ON "RefreshSessions"; DROP FUNCTION IF EXISTS api_test_reject_session();');
  }
  payload(await refresh(first.refreshToken));
});

test('SEC-03: refresh and replay evidence survive an API restart', async t => {
  const user = userFixture(t);
  const first = await user.login();
  const next = payload(await refresh(first.refreshToken));
  docker('restart', 'api');
  const address = docker('port', 'api', '8080').trim();
  assert.match(address, /^127\.0\.0\.1:\d+$/);
  const url = new URL(process.env.GRAPHQL_TEST_URL);
  url.host = address;
  process.env.GRAPHQL_TEST_URL = url.toString();
  let available = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    try {
      const result = await graphql('{ hello }', {}, next.accessToken);
      if (result.body.data?.hello) { available = true; break; }
    } catch { }
    await delay(200);
  }
  assert.ok(available, 'Restarted API must become ready');
  const afterRestart = payload(await refresh(next.refreshToken));
  denied(await refresh(first.refreshToken));
  denied(await refresh(afterRestart.refreshToken));
});
