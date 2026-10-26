import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { graphql, example, componentInput, expectErrors } from './helpers.mjs';

const loginDocument = `mutation($input:LoginInput!) { login(input:$input) { accessToken user { userId admin } } }`;
const addGuide = `mutation($input:ComponentGuideInput!) { addComponentGuide(input:$input) { componentId userId componentName feContent siO2Content al2O3Content caOContent mgOContent sContent mnOContent tiO2Content } }`;
const addVariant = `mutation($input:CalcVariantInput!,$components:[ComponentVariantInput!]!) { addCalcVariant(input:$input,components:$components) { variantId userId variantName } }`;
const updateVariant = `mutation($id:Int!,$input:CalcVariantInput!,$components:[ComponentVariantInput!]!) { updateCalcVariant(variantId:$id,input:$input,components:$components) { variantId userId variantName } }`;
const updateGuide = `mutation($id:Int!,$input:ComponentGuideInput!) { updateComponentGuide(componentId:$id,input:$input) { componentId userId componentName } }`;
const byId = `query($id:Int!) { calcVariantById(variantId:$id) { variantId userId variantName componentVariants { componentId componentName componentGuide { componentId userId componentName } calcVariant { variantId userId } } } }`;

async function success(query, variables, token) {
  const result = await graphql(query, variables, token);
  assert.equal(result.status, 200, JSON.stringify(result.body));
  assert.equal(result.body.errors, undefined, JSON.stringify(result.body.errors));
  return result.body.data;
}

export async function ownershipTokens() {
  const tokens = {};
  for (const [name, id, admin] of [['USER', -1, false], ['ADMIN', -2, true]]) {
    const email = process.env[`API_TEST_${name}_EMAIL`];
    const password = process.env[`API_TEST_${name}_PASSWORD`];
    assert.ok(email && password, `Missing API_TEST_${name} credentials`);
    const data = await success(loginDocument, { input: { email, password } }, null);
    assert.equal(data.login.user.userId, id);
    assert.equal(data.login.user.admin, admin);
    tokens[name.toLowerCase()] = data.login.accessToken;
  }
  return tokens;
}

test('OWN-01: two roles, server-assigned owners and SQL-filtered nested collections', async t => {
  const tokens = await ownershipTokens();
  const name = `ownership-${randomUUID()}`;
  const tracked = { variants: [], guides: [], furnace: null };
  t.after(async () => {
    for (const id of tracked.variants.toReversed())
      await success('mutation($id:Int!){deleteCalcVariant(variantId:$id)}', { id }, tokens.admin);
    for (const id of tracked.guides.toReversed())
      await success('mutation($id:Int!){deleteComponentGuide(componentId:$id)}', { id }, tokens.admin);
    if (tracked.furnace !== null)
      await success('mutation($id:Int!){deleteBlastFurnace(bfId:$id)}', { id: tracked.furnace }, tokens.admin);
  });
  const furnace = (await success('mutation($name:String!){addBlastFurnace(bfName:$name){bfId}}', { name }, tokens.admin)).addBlastFurnace;
  tracked.furnace = furnace.bfId;
  const guides = {};
  for (const role of ['user', 'admin']) {
    const input = { ...example('AddComponentGuide').input, componentName: `${name}-${role}`, userId: role === 'user' ? -2 : -1 };
    guides[role] = (await success(addGuide, { input }, tokens[role])).addComponentGuide;
    tracked.guides.push(guides[role].componentId);
    assert.equal(guides[role].userId, role === 'user' ? -1 : -2, 'client cannot assign guide owner');
  }
  const input = role => ({ ...example('AddCalcVariant').input, bfId: furnace.bfId, variantName: `${name}-${role}`, userId: role === 'user' ? -2 : -1 });
  const variants = {};
  for (const role of ['user', 'admin']) {
    // Admin may reference another owner's guide, but this must not make the variant visible through that guide.
    const components = [componentInput(guides.user)];
    variants[role] = (await success(addVariant, { input: input(role), components }, tokens[role])).addCalcVariant;
    tracked.variants.push(variants[role].variantId);
    assert.equal(variants[role].userId, role === 'user' ? -1 : -2, 'client cannot assign variant owner');
  }
  for (const role of ['user', 'admin']) {
    const data = await success(`{ calcVariants { variantId userId } componentGuides { componentId userId componentVariants { variantId calcVariant { userId } } } blastFurnaces { bfId calcVariants { variantId userId } } }`, {}, tokens[role]);
    if (role === 'user') {
      assert.ok(data.calcVariants.every(x => x.userId === -1));
      assert.ok(data.componentGuides.every(x => x.userId === -1));
      assert.ok(data.componentGuides.flatMap(x => x.componentVariants).every(x => x.calcVariant.userId === -1));
      assert.ok(data.blastFurnaces.flatMap(x => x.calcVariants).every(x => x.userId === -1));
      assert.ok(!data.calcVariants.some(x => x.variantId === variants.admin.variantId));
      assert.ok(!data.componentGuides.some(x => x.componentId === guides.admin.componentId));
      assert.equal((await success(byId, { id: variants.admin.variantId }, tokens.user)).calcVariantById, null);
    } else {
      assert.ok(data.calcVariants.some(x => x.variantId === variants.user.variantId));
      assert.ok(data.calcVariants.some(x => x.variantId === variants.admin.variantId));
      assert.ok(data.calcVariants.some(x => x.variantId === -1 && x.userId === -1));
      assert.ok(data.calcVariants.some(x => x.variantId === -2 && x.userId === -2));
    }
  }
  const own = (await success(byId, { id: variants.user.variantId }, tokens.user)).calcVariantById;
  assert.equal(own.componentVariants[0].componentGuide.userId, -1);
  const guideInput = { ...example('AddComponentGuide').input, componentName: `${name}-changed`, userId: -2 };
  assert.equal((await success(updateGuide, { id: guides.user.componentId, input: guideInput }, tokens.user)).updateComponentGuide.userId, -1);
  const changed = await success(updateVariant, { id: variants.user.variantId, input: input('admin'), components: [componentInput(guides.admin)] }, tokens.admin);
  assert.equal(changed.updateCalcVariant.userId, -1, 'admin editing a foreign record preserves its owner');
  const redacted = (await success(byId, { id: variants.user.variantId }, tokens.user)).calcVariantById;
  assert.equal(redacted.componentVariants[0].componentGuide, null, 'foreign guide metadata must not leak through an owned snapshot');
  assert.equal(redacted.componentVariants[0].calcVariant.userId, -1);
  const guideChangedByAdmin = await success(updateGuide, { id: guides.user.componentId, input: guideInput }, tokens.admin);
  assert.equal(guideChangedByAdmin.updateComponentGuide.userId, -1);

  assert.equal((await success(updateVariant, { id: variants.admin.variantId, input: input('user'), components: [] }, tokens.user)).updateCalcVariant, null);
  assert.equal((await success('mutation($id:Int!){deleteCalcVariant(variantId:$id)}', { id: variants.admin.variantId }, tokens.user)).deleteCalcVariant, false);
  assert.equal((await success(updateGuide, { id: guides.admin.componentId, input: guideInput }, tokens.user)).updateComponentGuide, null);
  assert.equal((await success('mutation($id:Int!){deleteComponentGuide(componentId:$id)}', { id: guides.admin.componentId }, tokens.user)).deleteComponentGuide, false);
  for (const query of [addVariant, updateVariant]) {
    const response = await graphql(query, { id: variants.user.variantId, input: input('user'), components: [componentInput(guides.admin)] }, tokens.user);
    expectErrors(response, { code: 'FORBIDDEN' });
  }
  for (const role of ['user', 'admin']) {
    expectErrors(await graphql(addVariant, { input: { ...input(role), isImport: true }, components: [] }, tokens[role]), { code: 'VALIDATION_FAILED' });
    expectErrors(await graphql(updateVariant, { id: variants[role].variantId, input: { ...input(role), isImport: true }, components: [] }, tokens[role]), { code: 'VALIDATION_FAILED' });
  }
  const unchanged = (await success(byId, { id: variants.user.variantId }, tokens.admin)).calcVariantById;
  assert.equal(unchanged.variantName, input('admin').variantName, 'rejected updates must not modify scalars or components');
  for (const [query, variables] of [
    ['mutation{addBlastFurnace(bfName:"forbidden"){bfId}}', {}],
    ['mutation($id:Int!){updateBlastFurnace(bfId:$id,bfName:"forbidden"){bfId}}', { id: furnace.bfId }],
    ['mutation($id:Int!){deleteBlastFurnace(bfId:$id)}', { id: furnace.bfId }],
  ]) expectErrors(await graphql(query, variables, tokens.user), { code: 'FORBIDDEN' });
});
