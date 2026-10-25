import test from 'node:test';
import assert from 'node:assert/strict';
import { componentInput, document, example, expectErrors, fixture, graphql, operation } from './helpers.mjs';

async function success(query, variables = {}) {
  const result = await graphql(query, variables);
  assert.equal(result.status, 200);
  assert.equal(result.body.errors, undefined, JSON.stringify(result.body.errors));
  return result.body.data;
}

const variantFields = `variantId bfId variantName saveTime parentId
  parent { variantId variantName }
  children { variantId variantName }
  blastFurnace { bfId bfName }
  componentVariants { variantId componentId componentName consumption
    calcVariant { variantId variantName bfId saveTime }
    componentGuide { componentId componentName userId feContent } }`;

const variantQuery = `query($id: Int!) { calcVariantById(variantId: $id) { ${variantFields} } }`;

test('saveTime is a stable UTC creation timestamp across contexts and updates; historical seed remains unknown', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const before = Date.now();
  const created = await f.variant(furnace.bfId);
  const after = Date.now();
  assert.ok(created.saveTime);
  assert.match(created.saveTime, /(?:Z|\+00:00)$/);
  assert.ok(Date.parse(created.saveTime) >= before - 1000 && Date.parse(created.saveTime) <= after + 1000);
  for (let i = 0; i < 2; i++) {
    assert.equal((await success(variantQuery, { id: created.variantId })).calcVariantById.saveTime, created.saveTime);
  }
  const input = { ...example('AddCalcVariant').input, bfId: furnace.bfId, variantName: `${created.variantName}-renamed` };
  const updated = (await operation('UpdateCalcVariant', { variantId: created.variantId, input, components: [] })).updateCalcVariant;
  assert.equal(updated.saveTime, created.saveTime);
  assert.equal((await success(variantQuery, { id: created.variantId })).calcVariantById.saveTime, created.saveTime);
  const seed = (await success(variantQuery, { id: -1 })).calcVariantById;
  assert.ok(seed);
  assert.equal(seed.saveTime, null);
});

test('all published relations contain their complete bounded data after separate requests', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const otherFurnace = await f.furnace();
  const guide = await f.guide();
  const otherGuide = await f.guide();
  const first = await f.variant(furnace.bfId, [componentInput(guide, { componentName: 'First snapshot' })]);
  const second = await f.variant(furnace.bfId, [componentInput(guide, { componentName: 'Second snapshot' })]);
  const unrelated = await f.variant(otherFurnace.bfId, [componentInput(otherGuide)]);
  const expectedIds = [first.variantId, second.variantId].sort((a, b) => a - b);

  const roots = await success(`{ blastFurnaces { bfId calcVariants { variantId bfId variantName } }
    componentGuides { componentId componentVariants { variantId componentId componentName
      calcVariant { variantId variantName }
      componentGuide { componentId componentName } } }
    calcVariants { ${variantFields} } }`);
  assert.deepEqual(roots.blastFurnaces.find(x => x.bfId === furnace.bfId).calcVariants.map(x => x.variantId).sort((a,b) => a-b), expectedIds);
  assert.deepEqual(roots.blastFurnaces.find(x => x.bfId === otherFurnace.bfId).calcVariants.map(x => x.variantId), [unrelated.variantId]);
  const guideRows = roots.componentGuides.find(x => x.componentId === guide.componentId).componentVariants;
  assert.deepEqual(guideRows.map(x => x.variantId).sort((a,b) => a-b), expectedIds);
  for (const row of guideRows) {
    assert.equal(row.componentGuide.componentId, guide.componentId);
    assert.equal(row.componentGuide.componentName, guide.componentName);
    assert.equal(row.calcVariant.variantId, row.variantId);
  }
  const single = (await success(variantQuery, { id: first.variantId })).calcVariantById;
  assert.deepEqual(single, roots.calcVariants.find(x => x.variantId === first.variantId));
  assert.equal(single.parent, null);
  assert.deepEqual(single.children, []);
  assert.equal(single.componentVariants[0].calcVariant.variantId, first.variantId);
  assert.equal(single.componentVariants[0].calcVariant.saveTime, single.saveTime);
  assert.equal(single.componentVariants[0].componentGuide.componentName, guide.componentName);
  assert.equal(single.componentVariants[0].componentName, 'First snapshot');

  const renamedFurnace = await success(`mutation($id: Int!, $name: String!) {
    updateBlastFurnace(bfId: $id, bfName: $name) { bfId calcVariants { variantId } }
  }`, { id: furnace.bfId, name: `${furnace.bfName}-renamed` });
  assert.deepEqual(renamedFurnace.updateBlastFurnace.calcVariants.map(x => x.variantId).sort((a,b) => a-b), expectedIds);
  const input = { ...example('UpdateComponentGuide').input, componentName: `${guide.componentName}-renamed` };
  const renamedGuide = await success(`mutation($id: Int!, $input: ComponentGuideInput!) {
    updateComponentGuide(componentId: $id, input: $input) { componentId componentVariants {
      variantId calcVariant { variantId } componentGuide { componentId componentName }
    } }
  }`, { id: guide.componentId, input });
  assert.deepEqual(renamedGuide.updateComponentGuide.componentVariants.map(x => x.variantId).sort((a,b) => a-b), expectedIds);
  assert.ok(renamedGuide.updateComponentGuide.componentVariants.every(x => x.componentGuide.componentName === input.componentName));
});

test('blank names and non-UTC dates reject before writing while absent updates preserve null contract', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const guide = await f.guide();
  const created = await f.variant(furnace.bfId, [componentInput(guide)]);
  const original = (await operation('CalcVariantById', { variantId: created.variantId })).calcVariantById;
  const input = { ...example('AddCalcVariant').input, bfId: furnace.bfId, variantName: created.variantName };
  for (const invalid of [
    { input: { ...input, variantName: ' \t ' }, components: [] },
    { input, components: [componentInput(guide, { componentName: ' ' })] },
    { input: { ...input, calcDate: '2026-06-17T12:34:56' }, components: [] },
  ]) {
    const response = await graphql(document('UpdateCalcVariant'), { variantId: created.variantId, ...invalid });
    // A timezone-free timestamp may be rejected by GraphQL's DateTime scalar before the resolver.
    expectErrors(response, invalid.input.calcDate.endsWith('56') ? {} : { code: 'VALIDATION_FAILED' });
    assert.deepEqual((await operation('CalcVariantById', { variantId: created.variantId })).calcVariantById, original);
  }
  for (const [name, variables] of [
    ['AddBlastFurnace', { bfName: ' ' }],
    ['UpdateBlastFurnace', { bfId: furnace.bfId, bfName: '' }],
    ['AddComponentGuide', { input: { ...example('AddComponentGuide').input, componentName: ' ' } }],
    ['UpdateComponentGuide', { componentId: guide.componentId, input: { ...example('UpdateComponentGuide').input, componentName: '' } }],
  ]) expectErrors(await graphql(document(name), variables), { code: 'VALIDATION_FAILED' });
  const missing = 2147483647;
  assert.equal((await operation('UpdateBlastFurnace', { bfId: missing, bfName: '' })).updateBlastFurnace, null);
  assert.equal((await operation('UpdateComponentGuide', { componentId: missing, input: { ...example('UpdateComponentGuide').input, componentName: '' } })).updateComponentGuide, null);
  assert.equal((await operation('UpdateCalcVariant', { variantId: missing, input: { ...input, variantName: '', bfId: missing }, components: [componentInput(guide), componentInput(guide)] })).updateCalcVariant, null);
});

test('stored slag sulfur round-trips nonzero values and omitted input preserves the legacy zero default', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const legacyDefault = await f.variant(furnace.bfId, [], { slagSContent: undefined });
  const created = await f.variant(furnace.bfId, [], { slagSContent: 0.135 });
  const read = async id => (await success(`query($id: Int!) {
    calcVariantById(variantId: $id) { variantId slagSContent saveTime }
  }`, { id })).calcVariantById;
  assert.equal((await read(legacyDefault.variantId)).slagSContent, 0);
  assert.ok(Math.abs((await read(created.variantId)).slagSContent - 0.135) < 1e-6);
  const input = { ...example('AddCalcVariant').input, bfId: furnace.bfId,
    variantName: created.variantName, slagSContent: 0.425 };
  const result = await success(`mutation($id: Int!, $input: CalcVariantInput!) {
    updateCalcVariant(variantId: $id, input: $input, components: []) { variantId slagSContent saveTime }
  }`, { id: created.variantId, input });
  assert.ok(Math.abs(result.updateCalcVariant.slagSContent - 0.425) < 1e-6);
  assert.equal(result.updateCalcVariant.saveTime, created.saveTime);
  assert.ok(Math.abs((await read(created.variantId)).slagSContent - 0.425) < 1e-6);
  const seed = await read(-1);
  assert.equal(seed.slagSContent, 0);
});
