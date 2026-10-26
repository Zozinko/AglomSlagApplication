import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { graphql, example, componentInput, expectErrors } from '../api/helpers.mjs';

const project = process.env.API_TEST_COMPOSE_PROJECT;
const composeFile = process.env.API_TEST_COMPOSE_FILE;
assert.match(project ?? '', /^aglom-api-tests-[a-f0-9]{12}$/);
assert.ok(composeFile);
const prefix = ['compose', '-p', project, '-f', composeFile];
const docker = (...args) => execFileSync('docker', [...prefix, ...args], { encoding: 'utf8', timeout: 180_000, maxBuffer: 8 * 1024 * 1024 });
const sql = (query, database = 'SlagMode') => execFileSync('docker', [...prefix, 'exec', '-T', 'postgres', 'psql', '-X', '-A', '-t', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', database], { input: query, encoding: 'utf8', timeout: 30_000 }).trim();
async function success(query, variables, token) {
  const result = await graphql(query, variables, token);
  assert.equal(result.status, 200, JSON.stringify(result.body));
  assert.equal(result.body.errors, undefined, JSON.stringify(result.body.errors));
  return result.body.data;
}
async function tokens() {
  const result = {};
  for (const role of ['USER', 'ADMIN']) {
    result[role.toLowerCase()] = (await success('mutation($input:LoginInput!){login(input:$input){accessToken}}', { input: { email: process.env[`API_TEST_${role}_EMAIL`], password: process.env[`API_TEST_${role}_PASSWORD`] } }, null)).login.accessToken;
  }
  return result;
}

const add = 'mutation($input:CalcVariantInput!,$components:[ComponentVariantInput!]!){addCalcVariant(input:$input,components:$components){variantId userId}}';
const update = 'mutation($id:Int!,$input:CalcVariantInput!,$components:[ComponentVariantInput!]!){updateCalcVariant(variantId:$id,input:$input,components:$components){variantId userId}}';
const remove = 'mutation($id:Int!){deleteCalcVariant(variantId:$id)}';

test('OWN-01: PostgreSQL filters mixed-owner parent/children and every bounded reference', async t => {
  const auth = await tokens();
  const label = `ownership-links-${randomUUID()}`;
  const furnace = (await success('mutation($name:String!){addBlastFurnace(bfName:$name){bfId}}', { name: label }, auth.admin)).addBlastFurnace;
  const guide = (await success('mutation($input:ComponentGuideInput!){addComponentGuide(input:$input){componentId componentName feContent siO2Content al2O3Content caOContent mgOContent sContent mnOContent tiO2Content}}', { input: { ...example('AddComponentGuide').input, componentName: label } }, auth.user)).addComponentGuide;
  const ids = [];
  t.after(async () => {
    if (ids.length) sql(`UPDATE "CalcVariants" SET "ParentId"=NULL WHERE "VariantId" IN (${ids.join(',')});`);
    for (const id of ids.toReversed()) await success(remove, { id }, auth.admin);
    await success('mutation($id:Int!){deleteComponentGuide(componentId:$id)}', { id: guide.componentId }, auth.admin);
    await success('mutation($id:Int!){deleteBlastFurnace(bfId:$id)}', { id: furnace.bfId }, auth.admin);
  });
  const variants = {};
  for (const role of ['user', 'admin']) {
    variants[role] = [];
    for (let i = 0; i < 2; i++) {
      const created = (await success(add, { input: { ...example('AddCalcVariant').input, bfId: furnace.bfId, variantName: `${label}-${role}-${i}` }, components: [componentInput(guide)] }, auth[role])).addCalcVariant;
      ids.push(created.variantId);
      variants[role].push(created.variantId);
    }
  }
  const [userParent, userChild] = variants.user;
  const [adminParent, adminChild] = variants.admin;
  sql(`UPDATE "CalcVariants" SET "ParentId"=${adminParent} WHERE "VariantId"=${userChild}; UPDATE "CalcVariants" SET "ParentId"=${userParent} WHERE "VariantId"=${adminChild};`);
  const query = `query($id:Int!){calcVariantById(variantId:$id){variantId parentId parent{variantId userId} children{variantId userId parentId} componentVariants{calcVariant{variantId parentId}}}}`;
  const child = (await success(query, { id: userChild }, auth.user)).calcVariantById;
  assert.equal(child.parent, null);
  assert.equal(child.parentId, null);
  assert.equal(child.componentVariants[0].calcVariant.parentId, null);
  const parent = (await success(query, { id: userParent }, auth.user)).calcVariantById;
  assert.deepEqual(parent.children, []);
  const full = (await success(query, { id: userChild }, auth.admin)).calcVariantById;
  assert.equal(full.parent.variantId, adminParent);
  assert.equal(full.parentId, adminParent);
  const adminRead = (await success(query, { id: userParent }, auth.admin)).calcVariantById;
  assert.deepEqual(adminRead.children.map(x => x.variantId), [adminChild]);
  const nested = await success('{blastFurnaces{calcVariants{variantId userId parentId}} componentGuides{componentVariants{calcVariant{variantId userId parentId}}}}', {}, auth.user);
  for (const ref of [...nested.blastFurnaces.flatMap(x => x.calcVariants), ...nested.componentGuides.flatMap(x => x.componentVariants.map(c => c.calcVariant))]) {
    assert.equal(ref.userId, -1);
    if (ref.variantId === userChild) assert.equal(ref.parentId, null);
  }
});

test('OWN-01: both roles cannot update or delete imported originals, including component replacement', async t => {
  const auth = await tokens();
  const label = `ownership-import-${randomUUID()}`;
  const ids = [];
  // These are private test fixtures marked as imports through SQL, never real imported data.
  t.after(async () => {
    if (ids.length) sql(`UPDATE "CalcVariants" SET "IsImport"=false WHERE "VariantId" IN (${ids.join(',')});`);
    for (const id of ids.toReversed()) await success(remove, { id }, auth.admin);
  });
  for (const role of ['user', 'admin']) {
    const input = { ...example('AddCalcVariant').input, bfId: -1, variantName: `${label}-${role}` };
    const guide = (await success('{componentGuides{componentId componentName feContent siO2Content al2O3Content caOContent mgOContent sContent mnOContent tiO2Content}}', {}, auth.user)).componentGuides.find(x => x.componentId === -1);
    assert.ok(guide);
    const variant = (await success(add, { input, components: [componentInput(guide)] }, auth[role])).addCalcVariant;
    ids.push(variant.variantId);
    sql(`UPDATE "CalcVariants" SET "IsImport"=true WHERE "VariantId"=${variant.variantId};`);
    const snapshot = () => sql(`SELECT jsonb_build_object('variant',to_jsonb(v),'components',(SELECT jsonb_agg(to_jsonb(c) ORDER BY c."ComponentId") FROM "ComponentVariants" c WHERE c."VariantId"=v."VariantId"))::text FROM "CalcVariants" v WHERE v."VariantId"=${variant.variantId};`);
    const before = snapshot();
    for (const caller of role === 'user' ? ['user', 'admin'] : ['admin']) {
      expectErrors(await graphql(update, { id: variant.variantId, input: { ...input, variantName: 'must-not-save', isImport: false }, components: [] }, auth[caller]), { code: 'IMPORTED_DATA_READ_ONLY' });
      expectErrors(await graphql(remove, { id: variant.variantId }, auth[caller]), { code: 'IMPORTED_DATA_DELETE_FORBIDDEN' });
      assert.equal(snapshot(), before, 'import scalars, owner and component rows must remain unchanged');
    }
  }
});

test('OWN-01: migrated owner columns/indexes and fixed demo identities agree across both databases', () => {
  assert.equal(sql(`SELECT is_nullable FROM information_schema.columns WHERE table_name='CalcVariants' AND column_name='UserId';`), 'NO');
  assert.equal(sql(`SELECT count(*) FROM pg_indexes WHERE tablename='CalcVariants' AND indexname='IX_CalcVariants_UserId';`), '1');
  assert.equal(sql(`SELECT count(*) FROM pg_indexes WHERE tablename='ComponentsGuide' AND indexname='IX_ComponentsGuide_UserId';`), '1');
  const users = JSON.parse(sql(`SELECT jsonb_agg(jsonb_build_object('id',"UserId",'admin',"Admin",'disabled',"Disabled") ORDER BY "UserId")::text FROM "Users" WHERE "UserId" IN (-1,-2);`, 'Auth'));
  assert.deepEqual(users, [{ id: -2, admin: true, disabled: false }, { id: -1, admin: false, disabled: false }]);
  const known = new Set(users.map(x => x.id));
  for (const table of ['CalcVariants', 'ComponentsGuide']) {
    const owners = sql(`SELECT DISTINCT "UserId" FROM "${table}" WHERE "UserId"<0 ORDER BY "UserId";`).split('\n').filter(Boolean).map(Number);
    assert.ok(owners.length);
    for (const owner of owners) assert.ok(known.has(owner), `${table} owner ${owner} absent in Auth`);
  }
  assert.equal(sql('SELECT "UserId" FROM "CalcVariants" WHERE "VariantId"=-1;'), '-1');
  assert.equal(sql('SELECT "UserId" FROM "CalcVariants" WHERE "VariantId"=-2;'), '-2');
  const before = sql('SELECT "UserId"::text || \'|\' || "PasswordHash" FROM "Users" WHERE "UserId" IN (-1,-2) ORDER BY "UserId";', 'Auth');
  docker('run', '--rm', '--no-deps', '-e', 'DEMO_USER_PASSWORD=ChangedCredentialMustNotReset1!', '-e', 'DEMO_ADMIN_PASSWORD=ChangedCredentialMustNotReset2!', 'api', '--bootstrap-demo');
  assert.ok(sql('SELECT "UserId"::text || \'|\' || "PasswordHash" FROM "Users" WHERE "UserId" IN (-1,-2) ORDER BY "UserId";', 'Auth') === before, 'idempotent bootstrap must preserve existing credentials');
});

test('OWN-01: legacy PostgreSQL upgrade backfills trustworthy owners without a permanent default', () => {
  const password = process.env.API_TEST_DB_PASSWORD;
  assert.ok(password);
  const connection = `Host=postgres;Database=ApiOwnershipUpgrade;Username=postgres;Password="${password.replaceAll('"', '""')}";Timeout=2`;
  const args = ['--context', 'SlagModeContext', '--project', 'DBStructure', '--startup-project', 'AglomSlagServer', '--connection', connection];
  sql('CREATE DATABASE "ApiOwnershipUpgrade";', 'postgres');
  docker('run', '--rm', '--no-deps', '--entrypoint', 'dotnet', 'migrator', 'ef', 'database', 'update', '20261025093431_AddSlagSulfurContent', ...args);
  sql(`INSERT INTO "CalcVariants" SELECT (jsonb_populate_record(NULL::"CalcVariants",to_jsonb(v)||'{"VariantId":101,"VariantName":"legacy-unowned"}'::jsonb)).* FROM "CalcVariants" v WHERE "VariantId"=-1; UPDATE "ComponentsGuide" SET "UserId"=12345;`, 'ApiOwnershipUpgrade');
  docker('run', '--rm', '--no-deps', '--entrypoint', 'dotnet', 'migrator', 'ef', 'database', 'update', ...args);
  assert.equal(sql('SELECT "UserId" FROM "CalcVariants" WHERE "VariantId"=101;', 'ApiOwnershipUpgrade'), '-1');
  assert.equal(sql('SELECT COUNT(*) FROM "CalcVariants";', 'ApiOwnershipUpgrade'), '3');
  assert.equal(sql('SELECT COUNT(*) FROM "ComponentsGuide" WHERE "UserId"<>-1;', 'ApiOwnershipUpgrade'), '0');
  assert.equal(sql(`SELECT column_default IS NULL FROM information_schema.columns WHERE table_name='CalcVariants' AND column_name='UserId';`, 'ApiOwnershipUpgrade'), 't');
  docker('run', '--rm', '--no-deps', '--entrypoint', 'dotnet', 'migrator', 'ef', 'database', 'update', ...args);
  assert.equal(sql('SELECT COUNT(*) FROM "CalcVariants";', 'ApiOwnershipUpgrade'), '3');
});


test('OWN-01: explicit demo bootstrap validates env, revives only legacy -1 and rejects identity/role collisions atomically', () => {
  const password = process.env.API_TEST_DB_PASSWORD;
  assert.ok(password);
  const database = 'ApiOwnershipBootstrap';
  const connection = `Host=postgres;Database=${database};Username=postgres;Password="${password.replaceAll('"', '""')}";Timeout=2`;
  sql(`CREATE DATABASE "${database}";`, 'postgres');
  docker('run', '--rm', '--no-deps', '--entrypoint', 'dotnet', 'migrator', 'ef', 'database', 'update', '--context', 'AuthContext', '--project', 'DBStructure', '--startup-project', 'AglomSlagServer', '--connection', connection);
  sql(`INSERT INTO "Users" ("UserId","Admin","Disabled","UserEmail","PasswordHash") VALUES (-1,false,true,'legacy@ownership.test','') ON CONFLICT ("UserId") DO UPDATE SET "Admin"=false,"Disabled"=true,"PasswordHash"='';`, database);
  const env = {
    ConnectionStrings__AuthContext: connection,
    ASPNETCORE_ENVIRONMENT: 'Development',
    DEMO_USER_EMAIL: 'demo-user@ownership.test',
    DEMO_ADMIN_EMAIL: 'demo-admin@ownership.test',
    DEMO_USER_PASSWORD: process.env.API_TEST_USER_PASSWORD,
    DEMO_ADMIN_PASSWORD: process.env.API_TEST_ADMIN_PASSWORD,
  };
  const bootstrap = (overrides = {}, expected = 0) => {
    const args = [...prefix, 'run', '--rm', '--no-deps'];
    for (const [key, value] of Object.entries({ ...env, ...overrides })) args.push('-e', `${key}=${value}`);
    args.push('api', '--bootstrap-demo');
    const result = spawnSync('docker', args, { encoding: 'utf8', timeout: 60_000, maxBuffer: 4 * 1024 * 1024 });
    assert.equal(result.error, undefined);
    if (expected === 0) assert.equal(result.status, 0, 'explicit demo bootstrap should succeed');
    else assert.notEqual(result.status, 0, 'invalid/colliding demo bootstrap must fail');
    const output = result.stdout + result.stderr;
    for (const secret of [env.DEMO_USER_PASSWORD, env.DEMO_ADMIN_PASSWORD, password])
      if (secret) assert.ok(!output.includes(secret), 'bootstrap output must not expose credentials');
  };
  for (const overrides of [
    { DEMO_USER_EMAIL: '' },
    { DEMO_ADMIN_EMAIL: 'invalid-address' },
    { DEMO_ADMIN_EMAIL: env.DEMO_USER_EMAIL },
    { DEMO_USER_PASSWORD: 'short' },
    { DEMO_USER_PASSWORD: ' '.repeat(12) },
    { DEMO_ADMIN_PASSWORD: 'x'.repeat(1025) },
  ]) bootstrap(overrides, 1);
  assert.equal(sql('SELECT "Disabled" FROM "Users" WHERE "UserId"=-1;', database), 't');
  bootstrap();
  assert.equal(sql('SELECT "Admin"::text || \'|\' || "Disabled"::text || \'|\' || (length("PasswordHash")>60)::text FROM "Users" WHERE "UserId"=-1;', database), 'false|false|true');
  assert.equal(sql('SELECT "Admin"::text || \'|\' || "Disabled"::text FROM "Users" WHERE "UserId"=-2;', database), 'true|false');
  const snapshot = () => sql('SELECT jsonb_agg(to_jsonb(u) ORDER BY "UserId")::text FROM "Users" u;', database);
  const before = snapshot();
  bootstrap({ DEMO_USER_EMAIL: 'different@ownership.test' }, 1);
  assert.ok(snapshot() === before, 'fixed-ID collision must leave both accounts unchanged');
  sql('UPDATE "Users" SET "Admin"=false WHERE "UserId"=-2;', database);
  const altered = snapshot();
  bootstrap({}, 1);
  assert.ok(snapshot() === altered, 'role collision must not silently promote an existing account');
  sql('UPDATE "Users" SET "Admin"=true WHERE "UserId"=-2;', database);
  sql(`INSERT INTO "Users" ("UserId","Admin","Disabled","UserEmail","PasswordHash") VALUES (111,false,true,'collision@ownership.test','');`, database);
  const collision = snapshot();
  bootstrap({ DEMO_USER_EMAIL: 'collision@ownership.test' }, 1);
  assert.ok(snapshot() === collision, 'email collision must leave every account unchanged');
});
