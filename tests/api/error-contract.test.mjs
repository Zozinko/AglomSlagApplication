import test from 'node:test';
import assert from 'node:assert/strict';
import { document, example, expectErrors, fixture, graphql } from './helpers.mjs';

const MISSING_ID = 2147483647;
const PRIVATE_MARKER = 'private-input-must-not-appear-in-errors';

function publicErrors(response, field, code) {
  const errors = expectErrors(response, { field, code });
  for (const error of errors) {
    assert.equal(typeof error.message, 'string');
    assert.ok(error.message.length > 0);
    assert.deepEqual(Object.keys(error.extensions ?? {}).sort(), ['code'],
      'Production resolver errors expose only the public error code');
    const serialized = JSON.stringify(error);
    assert.doesNotMatch(serialized,
      /stacktrace|stackTrace|System\.|Microsoft\.|Npgsql|DbUpdateException|ApiException|SELECT\s|INSERT\s|UPDATE\s|DELETE\s|Password=|Username=|Host=|ConnectionString/i);
    assert.ok(!serialized.includes(PRIVATE_MARKER), 'input values must not be echoed in errors');
  }
  return errors;
}

test('Production rejects invalid CRUD and calculation input with safe VALIDATION_FAILED errors', async () => {
  publicErrors(await graphql(document('AddBlastFurnace'), { bfName: '   ' }),
    'addBlastFurnace', 'VALIDATION_FAILED');
  const variables = example('CalculateSlagMode');
  variables.input.components = [];
  publicErrors(await graphql(document('CalculateSlagMode'), variables),
    'calculateSlagMode', 'VALIDATION_FAILED');
});

test('Production distinguishes a missing reference from validation without exposing input or SQL', async () => {
  const variables = example('AddCalcVariant');
  variables.input.bfId = MISSING_ID;
  variables.input.variantName = PRIVATE_MARKER;
  variables.components = [];
  publicErrors(await graphql(document('AddCalcVariant'), variables),
    'addCalcVariant', 'REFERENCE_NOT_FOUND');
});

test('Production reports a used reference as REFERENCE_CONFLICT with no database internals', async (t) => {
  const records = fixture(t);
  const furnace = await records.furnace(PRIVATE_MARKER);
  await records.variant(furnace.bfId);
  publicErrors(await graphql(document('DeleteBlastFurnace'), { bfId: furnace.bfId }),
    'deleteBlastFurnace', 'REFERENCE_CONFLICT');
});

test('Production distinguishes nonfinite mathematical output as safe CALCULATION_FAILED', async () => {
  const variables = example('CalculateSlagMode');
  // This finite, valid-shaped input produces a nonfinite DLL result, checked before serialization.
  for (const component of variables.input.components) component.fe = 0;
  publicErrors(await graphql(document('CalculateSlagMode'), variables),
    'calculateSlagMode', 'CALCULATION_FAILED');
});
