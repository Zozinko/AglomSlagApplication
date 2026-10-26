import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import {
  operation, example, document, graphql, fixture, componentInput, expectErrors,
} from './helpers.mjs';

function assertFields(actual, expected) {
  for (const [key, value] of Object.entries(expected)) {
    if (key === 'calcDate') {
      assert.equal(Date.parse(actual[key]), Date.parse(value), key);
      assert.match(actual[key], /(?:Z|\+00:00)$/);
    } else if (typeof value === 'number' && !key.endsWith('Id')) {
      assert.equal(typeof actual[key], 'number', key);
      assert.ok(Math.abs(actual[key] - value) <= Math.max(1e-6, Math.abs(value) * 1e-6),
        `${key}: expected ${value}, received ${actual[key]}`);
    } else {
      assert.deepEqual(actual[key], value, key);
    }
  }
}

function assertVariant(actual, input, components) {
  assertFields(actual, input);
  assert.ok(actual.variantId > 0);
  assert.equal(actual.parentId, null);
  assert.equal(actual.componentVariants.length, components.length);
  for (const component of components) {
    const stored = actual.componentVariants.find(c => c.componentId === component.componentId);
    assert.ok(stored, `Missing component ${component.componentId}`);
    assert.equal(stored.variantId, actual.variantId);
    assertFields(stored, component);
  }
}

async function readVariant(variantId) {
  return (await operation('CalcVariantById', { variantId })).calcVariantById;
}

function stableState(variant) {
  const state = variant;
  return {
    ...state,
    componentVariants: [...state.componentVariants].sort((a, b) => a.componentId - b.componentId),
  };
}

function changedInput(bfId, name = `api-variant-${randomUUID()}`) {
  const input = example('AddCalcVariant').input;
  // Distinct values for every mapped scalar catch omitted or crossed assignments.
  let index = 0;
  for (const key of Object.keys(input)) {
    if (typeof input[key] === 'number' && key !== 'bfId') input[key] += ++index / 100;
  }
  return { ...input, bfId, variantName: name, isImport: false, calcDate: '2026-06-17T12:34:56Z' };
}

test('calc variant CRUD round-trips every scalar and component, relationships and replacement lists', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const nextFurnace = await f.furnace();
  const guide = await f.guide();
  const nextGuide = await f.guide();
  const input = changedInput(furnace.bfId);
  const component = componentInput(guide, {
    componentName: 'Snapshot with independent composition', consumption: 1732.125,
    feContent: 52.125, siO2Content: 6.225, al2O3Content: 2.325,
    caOContent: 9.425, mgOContent: 3.525, sContent: 0.065,
    mnOContent: 0.275, tiO2Content: 0.385,
  });
  const created = await f.variant(furnace.bfId, [component], input);
  assertVariant(created, input, [component]);
  assert.deepEqual(created.blastFurnace, { bfId: furnace.bfId, bfName: furnace.bfName });

  const read = await readVariant(created.variantId);
  assertVariant(read, input, [component]);
  assert.deepEqual(read.blastFurnace, created.blastFurnace);
  const listed = (await operation('CalcVariants')).calcVariants.find(v => v.variantId === created.variantId);
  assert.ok(listed);
  assertVariant(listed, input, [component]);
  assert.deepEqual(listed.blastFurnace, created.blastFurnace);

  // Updating the guide must not mutate the saved component snapshot.
  const guideInput = { ...example('UpdateComponentGuide').input,
    componentName: 'Changed guide name', feContent: 1.25, siO2Content: 91.5 };
  await operation('UpdateComponentGuide', { componentId: guide.componentId, input: guideInput });
  assertVariant(await readVariant(created.variantId), input, [component]);

  const updatedInput = changedInput(nextFurnace.bfId);
  updatedInput.isImport = false;
  updatedInput.calcDate = '2026-07-18T01:02:03Z';
  for (const key of Object.keys(updatedInput)) {
    if (typeof updatedInput[key] === 'number' && key !== 'bfId') updatedInput[key] += 0.25;
  }
  const sameId = { ...component, componentName: 'Replaced same component ID', consumption: 987.65,
    feContent: 43.21, sContent: 0.0789 };
  const updated = (await operation('UpdateCalcVariant', {
    variantId: created.variantId, input: updatedInput, components: [sameId],
  })).updateCalcVariant;
  assertVariant(updated, updatedInput, [sameId]);
  const reread = await readVariant(created.variantId);
  assertVariant(reread, updatedInput, [sameId]);
  assert.deepEqual(reread.blastFurnace, { bfId: nextFurnace.bfId, bfName: nextFurnace.bfName });

  const replacement = componentInput(nextGuide, { consumption: 345.67 });
  assertVariant((await operation('UpdateCalcVariant', {
    variantId: created.variantId, input: updatedInput, components: [replacement],
  })).updateCalcVariant, updatedInput, [replacement]);
  assertVariant(await readVariant(created.variantId), updatedInput, [replacement]);

  assertVariant((await operation('UpdateCalcVariant', {
    variantId: created.variantId, input: updatedInput, components: [],
  })).updateCalcVariant, updatedInput, []);
  assertVariant(await readVariant(created.variantId), updatedInput, []);

  // Reintroduce children so deletion verifies the component cascade as well.
  await operation('UpdateCalcVariant', {
    variantId: created.variantId, input: updatedInput, components: [replacement],
  });
  assert.equal((await operation('DeleteCalcVariant', { variantId: created.variantId })).deleteCalcVariant, true);
  assert.equal(await readVariant(created.variantId), null);
  assert.ok(!(await operation('CalcVariants')).calcVariants.some(v => v.variantId === created.variantId));
  assert.equal((await operation('UpdateCalcVariant', {
    variantId: created.variantId, input: updatedInput, components: [],
  })).updateCalcVariant, null);
  assert.equal((await operation('DeleteCalcVariant', { variantId: created.variantId })).deleteCalcVariant, false);
  assert.equal((await operation('DeleteComponentGuide', { componentId: nextGuide.componentId })).deleteComponentGuide, true);
});

test('invalid furnace, component FK and duplicate components reject atomically on add and update', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const missingFurnace = await f.furnace();
  await operation('DeleteBlastFurnace', { bfId: missingFurnace.bfId });
  const guide = await f.guide();
  const secondGuide = await f.guide();
  const missingGuide = await f.guide();
  await operation('DeleteComponentGuide', { componentId: missingGuide.componentId });
  const component = componentInput(guide);
  const second = componentInput(secondGuide, { consumption: 222.25 });
  const missingComponent = componentInput(missingGuide);
  const created = await f.variant(furnace.bfId, [component, second]);
  const original = stableState(await readVariant(created.variantId));

  const cases = [
    { name: 'missing furnace', bfId: missingFurnace.bfId, components: [component] },
    { name: 'missing component FK', bfId: furnace.bfId, components: [second, missingComponent] },
    { name: 'duplicate component', bfId: furnace.bfId, components: [second, { ...second, consumption: 123 }] },
  ];
  for (const invalid of cases) {
    await t.test(invalid.name, async () => {
      const input = changedInput(invalid.bfId);
      const add = await graphql(document('AddCalcVariant'), { input, components: invalid.components });
      // Cleanup even an unexpected partial insert before failing the assertion.
      const unexpected = (await operation('CalcVariants')).calcVariants.filter(v => v.variantName === input.variantName);
      for (const variant of unexpected) await operation('DeleteCalcVariant', { variantId: variant.variantId });
      expectErrors(add, { field: 'addCalcVariant', code: invalid.name === 'duplicate component' ? 'VALIDATION_FAILED' : invalid.name === 'missing component FK' ? 'FORBIDDEN' : 'REFERENCE_NOT_FOUND' });
      assert.equal(unexpected.length, 0, 'Failed add must not persist a variant or its children');
      assert.deepEqual(stableState(await readVariant(created.variantId)), original);

      const update = await graphql(document('UpdateCalcVariant'), {
        variantId: created.variantId, input, components: invalid.components,
      });
      expectErrors(update, { field: 'updateCalcVariant', code: invalid.name === 'duplicate component' ? 'VALIDATION_FAILED' : invalid.name === 'missing component FK' ? 'FORBIDDEN' : 'REFERENCE_NOT_FOUND' });
      assert.deepEqual(stableState(await readVariant(created.variantId)), original,
        'Failed update must preserve all original scalars, furnace and components');
    });
  }
});

test('update returns the actual furnace with the same and a different furnace ID', async t => {
  const f = fixture(t);
  const furnace = await f.furnace();
  const other = await f.furnace();
  const created = await f.variant(furnace.bfId);
  const query = document('UpdateCalcVariant');
  for (const target of [furnace, other]) {
    const input = changedInput(target.bfId);
    const response = await graphql(query, { variantId: created.variantId, input, components: [] });
    assert.equal(response.body.errors, undefined, JSON.stringify(response.body.errors));
    assertVariant(response.body.data.updateCalcVariant, input, []);
    assert.deepEqual(response.body.data.updateCalcVariant.blastFurnace, target);
    assert.deepEqual((await readVariant(created.variantId)).blastFurnace, target);
  }
});
