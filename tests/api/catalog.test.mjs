import test from 'node:test';
import assert from 'node:assert/strict';

import {
  componentInput,
  document,
  expectErrors,
  fixture,
  graphql,
  operation,
} from './helpers.mjs';

const MISSING_ID = 2147483647;

function assertGuide(actual, expected, componentId) {
  assert.equal(actual.componentId, componentId);
  assert.equal(actual.componentName, expected.componentName);
  assert.equal(actual.userId, expected.userId);
  for (const field of [
    'feContent', 'siO2Content', 'al2O3Content', 'caOContent',
    'mgOContent', 'sContent', 'mnOContent', 'tiO2Content',
  ]) {
    assert.ok(
      Math.abs(actual[field] - expected[field]) < 1e-5,
      `${field}: expected ${expected[field]}, received ${actual[field]}`,
    );
  }
}

test('blast furnace CRUD, list visibility, missing IDs, and reference protection', async (t) => {
  const records = fixture(t);
  const furnace = await records.furnace();
  assert.ok(Number.isInteger(furnace.bfId));
  assert.equal(typeof furnace.bfName, 'string');

  let furnaces = (await operation('BlastFurnaces')).blastFurnaces;
  assert.ok(furnaces.some(({ bfId, bfName }) => bfId === furnace.bfId && bfName === furnace.bfName));

  const updatedName = `${furnace.bfName}-updated`;
  const updated = (await operation('UpdateBlastFurnace', {
    bfId: furnace.bfId,
    bfName: updatedName,
  })).updateBlastFurnace;
  assert.deepEqual(updated, { bfId: furnace.bfId, bfName: updatedName });

  furnaces = (await operation('BlastFurnaces')).blastFurnaces;
  assert.ok(furnaces.some(({ bfId, bfName }) => bfId === furnace.bfId && bfName === updatedName));

  assert.equal((await operation('UpdateBlastFurnace', {
    bfId: MISSING_ID,
    bfName: 'missing-furnace-update',
  })).updateBlastFurnace, null);
  assert.equal((await operation('DeleteBlastFurnace', { bfId: MISSING_ID })).deleteBlastFurnace, false);

  // A stored variant must prevent deleting its furnace until the variant is removed.
  const variant = await records.variant(furnace.bfId);
  const blocked = await graphql(document('DeleteBlastFurnace'), { bfId: furnace.bfId });
  assert.equal(blocked.status, 200);
  expectErrors(blocked, { field: 'deleteBlastFurnace', code: 'REFERENCE_CONFLICT' });
  furnaces = (await operation('BlastFurnaces')).blastFurnaces;
  assert.ok(furnaces.some(({ bfId, bfName }) => bfId === furnace.bfId && bfName === updatedName));

  assert.equal((await operation('DeleteCalcVariant', { variantId: variant.variantId })).deleteCalcVariant, true);
  assert.equal((await operation('DeleteBlastFurnace', { bfId: furnace.bfId })).deleteBlastFurnace, true);
  furnaces = (await operation('BlastFurnaces')).blastFurnaces;
  assert.ok(!furnaces.some(({ bfId }) => bfId === furnace.bfId));
});

test('component guide CRUD, list visibility, missing IDs, and reference protection', async (t) => {
  const records = fixture(t);
  const input = {
    componentName: `catalog-guide-${Date.now()}-${Math.random().toString(16).slice(2)}`,
    userId: -731,
    feContent: 57.1234,
    siO2Content: 6.2345,
    al2O3Content: 2.3456,
    caOContent: 9.4567,
    mgOContent: 1.5678,
    sContent: 0.0345,
    mnOContent: 0.4567,
    tiO2Content: 0.5678,
  };
  const guide = await records.guide(input);
  assertGuide(guide, input, guide.componentId);

  let guides = (await operation('ComponentGuides')).componentGuides;
  assertGuide(guides.find(({ componentId }) => componentId === guide.componentId), input, guide.componentId);

  const updatedInput = {
    componentName: `${input.componentName}-updated`,
    userId: 842,
    feContent: 48.8765,
    siO2Content: 7.7654,
    al2O3Content: 3.6543,
    caOContent: 10.5432,
    mgOContent: 2.4321,
    sContent: 0.0432,
    mnOContent: 0.5432,
    tiO2Content: 0.6543,
  };
  const updated = (await operation('UpdateComponentGuide', {
    componentId: guide.componentId,
    input: updatedInput,
  })).updateComponentGuide;
  assertGuide(updated, updatedInput, guide.componentId);

  guides = (await operation('ComponentGuides')).componentGuides;
  assertGuide(guides.find(({ componentId }) => componentId === guide.componentId), updatedInput, guide.componentId);

  assert.equal((await operation('UpdateComponentGuide', {
    componentId: MISSING_ID,
    input: updatedInput,
  })).updateComponentGuide, null);
  assert.equal((await operation('DeleteComponentGuide', { componentId: MISSING_ID })).deleteComponentGuide, false);

  const furnace = await records.furnace();
  const variant = await records.variant(furnace.bfId, [componentInput(guide)]);
  const blocked = await graphql(document('DeleteComponentGuide'), { componentId: guide.componentId });
  assert.equal(blocked.status, 200);
  expectErrors(blocked, { field: 'deleteComponentGuide', code: 'REFERENCE_CONFLICT' });
  guides = (await operation('ComponentGuides')).componentGuides;
  assertGuide(guides.find(({ componentId }) => componentId === guide.componentId), updatedInput, guide.componentId);

  assert.equal((await operation('DeleteCalcVariant', { variantId: variant.variantId })).deleteCalcVariant, true);
  assert.equal((await operation('DeleteComponentGuide', { componentId: guide.componentId })).deleteComponentGuide, true);
  guides = (await operation('ComponentGuides')).componentGuides;
  assert.ok(!guides.some(({ componentId }) => componentId === guide.componentId));
});
