import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';
import { fixture, operation, example, graphql, document, expectErrors } from '../api/helpers.mjs';

const project = process.env.API_TEST_COMPOSE_PROJECT;
const composeFile = process.env.API_TEST_COMPOSE_FILE;
assert.match(project ?? '', /^aglom-api-tests-[a-f0-9]{12}$/, 'Infrastructure tests require the generated isolated project');
assert.ok(composeFile);
const prefix = ['compose', '-p', project, '-f', composeFile];
const docker = (...args) => execFileSync('docker', [...prefix, ...args], { encoding: 'utf8', timeout: 180_000, maxBuffer: 8 * 1024 * 1024 });
const sql = (query, database = 'SlagMode') => execFileSync('docker', [...prefix, 'exec', '-T', 'postgres', 'psql', '-X', '-A', '-t', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', database], {
  input: query, encoding: 'utf8', timeout: 30_000,
}).trim();
const password = process.env.API_TEST_DB_PASSWORD;
assert.ok(password);
const connection = (database, credential = password) => `Host=postgres;Database=${database};Username=postgres;Password="${credential.replaceAll('"', '""')}";Timeout=2`;
const efArgs = ['--context', 'SlagModeContext', '--project', 'DBStructure', '--startup-project', 'AglomSlagServer'];
const ef = (...args) => docker('run', '--rm', '--no-deps', '--entrypoint', 'dotnet', 'migrator', 'ef', ...args);

async function ready(endpoint = process.env.GRAPHQL_TEST_URL) {
  for (let attempt = 0; attempt < 100; attempt++) {
    try {
      const response = await fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json', authorization: `Bearer ${process.env.API_TEST_ACCESS_TOKEN}` }, body: '{"query":"{ hello }"}', signal: AbortSignal.timeout(1000) });
      if ((await response.json()).data?.hello === 'Aglom GraphQL API') return;
    } catch { }
    await delay(200);
  }
  assert.fail(`API did not become ready: ${endpoint}`);
}

test('BUG-01: SQL timestamp, unknown legacy time, stable reads, update and API restart', async t => {
  assert.equal(sql(`SELECT data_type || ':' || is_nullable FROM information_schema.columns WHERE table_name='CalcVariants' AND column_name='SaveTime';`), 'timestamp with time zone:YES');
  assert.equal(sql('SELECT "SaveTime" IS NULL FROM "CalcVariants" WHERE "VariantId"=-1;'), 't');
  const f = fixture(t);
  const furnace = await f.furnace();
  const before = Date.now();
  const variant = await f.variant(furnace.bfId);
  assert.ok(Date.parse(variant.saveTime) >= before - 1000);
  assert.ok(Date.parse(variant.saveTime) <= Date.now() + 1000);
  assert.match(variant.saveTime, /(?:Z|\+00:00)$/);
  const sqlBefore = sql(`SELECT "SaveTime"::text FROM "CalcVariants" WHERE "VariantId"=${variant.variantId};`);
  const read = async () => (await operation('CalcVariantById', { variantId: variant.variantId })).calcVariantById;
  assert.equal((await read()).saveTime, variant.saveTime);
  const input = { ...example('AddCalcVariant').input, bfId: furnace.bfId, variantName: `${variant.variantName}-renamed` };
  await operation('UpdateCalcVariant', { variantId: variant.variantId, input, components: [] });
  assert.equal((await read()).saveTime, variant.saveTime);
  docker('restart', 'api');
  // Docker Desktop may assign a different ephemeral host port on restart.
  const publishedPort = docker('port', 'api', '8080').trim();
  assert.match(publishedPort, /^127\.0\.0\.1:\d+$/);
  process.env.GRAPHQL_TEST_URL = `http://${publishedPort}/graphql`;
  await ready();
  const after = await read();
  assert.equal(after.saveTime, variant.saveTime);
  assert.equal(after.variantName, input.variantName);
  assert.equal(sql(`SELECT "SaveTime"::text FROM "CalcVariants" WHERE "VariantId"=${variant.variantId};`), sqlBefore);
});

test('BUG-04/09: real parent/children links are bounded, and parent deletion is a public conflict', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const parent = await f.variant(furnace.bfId);
  const child = await f.variant(furnace.bfId);
  sql(`UPDATE "CalcVariants" SET "ParentId"=${parent.variantId} WHERE "VariantId"=${child.variantId};`);
  const data = await graphql(`query($id:Int!) { calcVariantById(variantId:$id) { variantId children { variantId parentId } } }`, { id: parent.variantId });
  assert.equal(data.body.errors, undefined, JSON.stringify(data.body));
  assert.deepEqual(data.body.data.calcVariantById.children.map(c => c.variantId), [child.variantId]);
  const childData = await graphql(`query($id:Int!) { calcVariantById(variantId:$id) { parentId parent { variantId } } }`, { id: child.variantId });
  assert.equal(childData.body.errors, undefined, JSON.stringify(childData.body));
  assert.equal(childData.body.data.calcVariantById.parent.variantId, parent.variantId);
  const blocked = await graphql(document('DeleteCalcVariant'), { variantId: parent.variantId });
  expectErrors(blocked, { code: 'REFERENCE_CONFLICT', field: 'deleteCalcVariant' });
  assert.equal(sql(`SELECT COUNT(*) FROM "CalcVariants" WHERE "VariantId" IN (${parent.variantId},${child.variantId});`), '2');
});

test('BUG-01/11: migrate a previous schema with existing rows, repeat migration, and check model snapshot', () => {
  sql('CREATE DATABASE "ApiUpgrade";', 'postgres');
  ef('database', 'update', '20261018094542_EditReferences', ...efArgs, '--connection', connection('ApiUpgrade'));
  sql(`INSERT INTO "CalcVariants" SELECT (jsonb_populate_record(NULL::"CalcVariants", to_jsonb(v) || '{"VariantId":101,"VariantName":"legacy-before-upgrade"}'::jsonb)).* FROM "CalcVariants" v WHERE "VariantId"=-1;`, 'ApiUpgrade');
  ef('database', 'update', ...efArgs, '--connection', connection('ApiUpgrade'));
  assert.equal(sql('SELECT COUNT(*) FROM "CalcVariants";', 'ApiUpgrade'), '3');
  assert.equal(sql('SELECT COUNT(*) FROM "CalcVariants" WHERE "SaveTime" IS NULL;', 'ApiUpgrade'), '3');
  assert.equal(sql('SELECT COUNT(*) FROM "CalcVariants" WHERE "SlagSContent"=0;', 'ApiUpgrade'), '3');
  ef('database', 'update', ...efArgs, '--connection', connection('ApiUpgrade'));
  assert.equal(sql('SELECT COUNT(*) FROM "CalcVariants" WHERE "SaveTime" IS NULL;', 'ApiUpgrade'), '3');
  ef('migrations', 'has-pending-model-changes', ...efArgs);
});

test('BUG-09: a database failure during component replacement rolls back scalars and deleted children', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const first = await f.guide();
  const second = await f.guide();
  const component = guide => ({ ...example('AddCalcVariant').components[0], componentId: guide.componentId, componentName: guide.componentName });
  const variant = await f.variant(furnace.bfId, [component(first)]);
  const original = (await operation('CalcVariantById', { variantId: variant.variantId })).calcVariantById;
  try {
    // Valid references pass API preflight; force a real PostgreSQL constraint error
    // only when saving the new child, after the original composition is removed.
    sql(`CREATE FUNCTION api_test_reject_component() RETURNS trigger LANGUAGE plpgsql AS $$
      BEGIN RAISE EXCEPTION 'forced test conflict' USING ERRCODE = '23505'; END $$;
      CREATE TRIGGER api_test_component_failure BEFORE INSERT OR UPDATE ON "ComponentVariants"
      FOR EACH ROW WHEN (NEW."VariantId"=${variant.variantId}) EXECUTE FUNCTION api_test_reject_component();`);
    const input = { ...example('AddCalcVariant').input, bfId: furnace.bfId, variantName: `${variant.variantName}-must-rollback` };
    const response = await graphql(document('UpdateCalcVariant'), { variantId: variant.variantId, input, components: [component(second)] });
    expectErrors(response, { field: 'updateCalcVariant', code: 'REFERENCE_CONFLICT' });
    assert.doesNotMatch(JSON.stringify(response.body), /forced test conflict|23505|Npgsql|INSERT |UPDATE |stackTrace/);
    const restored = (await operation('CalcVariantById', { variantId: variant.variantId })).calcVariantById;
    assert.deepEqual(restored, original);
    assert.equal(sql(`SELECT "ComponentId" FROM "ComponentVariants" WHERE "VariantId"=${variant.variantId};`), String(first.componentId));
  } finally {
    sql('DROP TRIGGER IF EXISTS api_test_component_failure ON "ComponentVariants"; DROP FUNCTION IF EXISTS api_test_reject_component();');
  }
});

function failStartup(overrides) {
  const args = [...prefix, 'run', '--rm', '--no-deps'];
  for (const [name, value] of Object.entries(overrides)) args.push('-e', `${name}=${value}`);
  args.push('api');
  const result = spawnSync('docker', args, { encoding: 'utf8', timeout: 45_000, maxBuffer: 4 * 1024 * 1024 });
  assert.equal(result.error, undefined, result.error?.message);
  assert.notEqual(result.status, 0, 'Invalid startup must fail');
  const logs = result.stdout + result.stderr;
  assert.doesNotMatch(logs, /Now listening on:|Подключение к базам данных успешно/);
  assert.ok(!logs.includes(password), 'Logs must not contain the test credential');
  assert.doesNotMatch(logs, /definitely-wrong-password|Password=|Username=|Host=/i);
  return logs;
}

test('BUG-06/07: wrong password for either DB prevents startup without revealing credentials', () => {
  for (const database of ['SlagMode', 'Auth']) {
    const logs = failStartup({ [`ConnectionStrings__${database}Context`]: connection(database, 'definitely-wrong-password') });
    assert.match(logs, new RegExp(`${database}Context`));
  }
});

test('BUG-06/07: malformed connection configuration also fails without echoing its contents', () => {
  const logs = failStartup({ ConnectionStrings__SlagModeContext: 'bad-key=definitely-wrong-password' });
  assert.match(logs, /SlagModeContext/);
  assert.doesNotMatch(logs, /bad-key/);
});

test('BUG-06: migrator exits nonzero on a failed database connection', () => {
  const result = spawnSync('docker', [...prefix, 'run', '--rm', '--no-deps', '-e', `CONNECTION_SLAG=${connection('SlagMode', 'definitely-wrong-password')}`, 'migrator'], {
    encoding: 'utf8', timeout: 90_000, maxBuffer: 4 * 1024 * 1024,
  });
  assert.equal(result.error, undefined, result.error?.message);
  assert.notEqual(result.status, 0);
  assert.ok(!(result.stdout + result.stderr).includes(password));
  assert.doesNotMatch(result.stdout + result.stderr, /definitely-wrong-password/);
});

test('BUG-06/07: Development diagnostics remain safe and actual alternate port is logged', async () => {
  const name = `${project}-development-check`;
  assert.match(name, /^aglom-api-tests-[a-f0-9]{12}-development-check$/);
  try {
    docker('run', '-d', '--no-deps', '--name', name, '-p', '127.0.0.1::5093', '-e', 'ASPNETCORE_ENVIRONMENT=Development', '-e', 'ASPNETCORE_URLS=http://+:5093', 'api');
    const port = execFileSync('docker', ['port', name, '5093/tcp'], { encoding: 'utf8' }).trim();
    assert.match(port, /^127\.0\.0\.1:\d+$/);
    const endpoint = `http://${port}/graphql`;
    await ready(endpoint);
    const response = await fetch(endpoint, { method: 'POST', headers: { 'content-type': 'application/json', authorization: `Bearer ${process.env.API_TEST_ACCESS_TOKEN}` }, body: JSON.stringify({ query: document('AddBlastFurnace'), variables: { bfName: ' ' } }), signal: AbortSignal.timeout(10_000) });
    const body = await response.json();
    assert.equal(body.errors?.[0]?.extensions?.code, 'VALIDATION_FAILED', JSON.stringify(body));
    assert.doesNotMatch(JSON.stringify(body), /stackTrace|Password|api-tests-only|Npgsql|SELECT |INSERT /i);
    const logs = execFileSync('docker', ['logs', name], { encoding: 'utf8' });
    assert.match(logs, /5093/);
    assert.doesNotMatch(logs, /слушает порт 8080/);
  } finally {
    execFileSync('docker', ['rm', '-f', name], { encoding: 'utf8' });
  }
});

test('BUG-06/07: stopped PostgreSQL prevents startup and runtime DB failures stay private', async () => {
  // Last infrastructure test; the isolated database is removed immediately after it.
  docker('stop', 'postgres');
  const logs = failStartup({
    ConnectionStrings__SlagModeContext: connection('SlagMode'),
    ConnectionStrings__AuthContext: connection('Auth'),
  });
  assert.match(logs, /SlagModeContext|AuthContext/);
  const response = await graphql('{ blastFurnaces { bfId } }');
  expectErrors(response, { code: 'INTERNAL_ERROR', field: 'blastFurnaces' });
  assert.doesNotMatch(JSON.stringify(response.body), /stackTrace|exceptionType|Npgsql|SELECT |Password|api-tests-only|ConnectionString/i);
  const apiLogs = docker('logs', 'api');
  assert.doesNotMatch(apiLogs, /Password=|api-tests-only|definitely-wrong-password|SELECT .*FROM|INSERT INTO| at Npgsql/i);
});
