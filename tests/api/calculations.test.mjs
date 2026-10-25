import test from 'node:test';
import assert from 'node:assert/strict';

import { document, example, expectErrors, graphql, operation } from './helpers.mjs';

const AGLOM_NUMERIC_FIELDS = [
  'reportComponentOfCharge', 'reportFe', 'reportS', 'reportP', 'reportFeO',
  'reportFe2O3', 'reportCaO', 'reportSiO2', 'reportAl2O3', 'reportMgO',
  'reportMnO', 'reportTiO2', 'reportZn', 'reportPMPP', 'reportCaO_SiO2',
  'reportOxideSum',
];

const SLAG_NUMERIC_FIELDS = [
  'slagBasicity1', 'slagBasicity2', 'slagBasicity3', 'slagBasicityKulikov',
  'slagOut', 'materialCons', 'totalMat', 'viscosity1400', 'viscosity1450',
  'viscosity1500', 'viscosity1550', 'temp7Puaz', 'gradient725',
  'gradient14001500', 'slagTemperature', 'slagTemperature25Puaz',
  'currSlagViscosity', 'balSlagMass', 'caOBalSlagMass', 'totalSInOre',
  'sActivity', 'sDistribution', 'sContentInCastIron', 'castIronTemp',
];

function assertFiniteFields(value, fields, context) {
  for (const field of fields) {
    assert.equal(typeof value[field], 'number', `${context}.${field} should serialize as a number`);
    assert.ok(Number.isFinite(value[field]), `${context}.${field} should be finite`);
  }
}

function sum(values) {
  return values.reduce((total, value) => total + value, 0);
}

function assertMaterialParts(result, materials) {
  assert.ok(Array.isArray(result.materialParts), 'materialParts should be a list');
  assert.equal(result.materialParts.length, materials.length);

  const total = sum(materials.map(({ consumption }) => consumption));
  assert.ok(total > 0, 'fixture must have a positive total consumption');
  const expectedParts = new Map(materials.map(({ sourcename, consumption }) => [
    sourcename,
    consumption / total,
  ]));
  const seenNames = [];
  const actualFractions = [];

  for (const [index, part] of result.materialParts.entries()) {
    assert.ok(Array.isArray(part), `materialParts[${index}] should be a list`);
    assert.equal(part.length, 1, `materialParts[${index}] should contain one key/value pair`);
    const [entry] = part;
    assert.equal(typeof entry.key, 'string');
    assert.equal(typeof entry.value, 'number');
    assert.ok(Number.isFinite(entry.value));
    assert.ok(expectedParts.has(entry.key), `unexpected material name ${entry.key}`);
    assert.ok(!seenNames.includes(entry.key), `duplicate material name ${entry.key}`);
    assert.ok(Math.abs(entry.value - expectedParts.get(entry.key)) < 1e-6,
      `share for ${entry.key} should be its input consumption divided by total consumption`);
    seenNames.push(entry.key);
    actualFractions.push(entry.value);
  }

  assert.deepEqual([...seenNames].sort(), [...expectedParts.keys()].sort());
  assert.ok(Math.abs(sum(actualFractions) - 1) < 1e-6, 'material fractions should sum to 1');
  assert.ok(Math.abs(result.totalMat - total) < 1e-5, 'totalMat should equal summed input consumption');
}

test('calculateAglom serializes the selected report and echoes the supplied userId for both flux types', async () => {
  const base = example('CalculateAglom');

  for (const [index, fluxType] of ['LIMESTONE', 'DOLOMITE'].entries()) {
    const variables = structuredClone(base);
    variables.input.userId = 731_000 + index;
    variables.input.aglomParameters.fluxType = fluxType;

    const result = (await operation('CalculateAglom', variables)).calculateAglom;
    assert.equal(result.id, variables.input.userId);
    assert.equal(result.message, 'Расчёт выполнен');
    assert.ok(Array.isArray(result.components));
    assert.ok(result.components.length > 0, 'calculation should return report rows');

    for (const [rowIndex, row] of result.components.entries()) {
      assert.equal(typeof row.componentName, 'string');
      assert.ok(row.componentName.length > 0);
      assertFiniteFields(row, AGLOM_NUMERIC_FIELDS, `components[${rowIndex}]`);
    }
  }
});

test('calculateSlagMode returns finite selected results and input-derived material shares', async () => {
  const variables = example('CalculateSlagMode');
  const inputMaterials = variables.input.components;
  const result = (await operation('CalculateSlagMode', variables)).calculateSlagMode;

  assertFiniteFields(result, SLAG_NUMERIC_FIELDS, 'calculateSlagMode');
  assertMaterialParts(result, inputMaterials);
});

test('calculateSlagMode rejects empty, zero-total, and negative-total material lists during execution', async (t) => {
  const cases = [
    ['empty components', (input) => { input.components = []; }],
    ['zero total consumption', (input) => {
      input.components = input.components.map((material) => ({ ...material, consumption: 0 }));
    }],
    ['negative total consumption', (input) => {
      input.components = input.components.map((material) => ({ ...material, consumption: -1 }));
    }],
  ];

  for (const [label, mutate] of cases) {
    await t.test(label, async () => {
      const variables = example('CalculateSlagMode');
      mutate(variables.input);
      const response = await graphql(document('CalculateSlagMode'), variables);
      expectErrors(response, { field: 'calculateSlagMode' });
      assert.ok(response.body.data?.calculateSlagMode == null);
      assert.ok(response.body.errors.every((error) => error.path?.includes('calculateSlagMode')),
        'service validation should be reported as resolver execution errors with a response path');
    });
  }
});

test('calculateSlagMode rejects a null components list during GraphQL input coercion', async () => {
  const variables = example('CalculateSlagMode');
  variables.input.components = null;
  const response = await graphql(document('CalculateSlagMode'), variables);
  expectErrors(response);
  assert.equal(response.body.data, undefined);
  assert.ok(response.body.errors.every((error) => error.path === undefined),
    'a null non-null GraphQL list should fail before resolver execution');
});
