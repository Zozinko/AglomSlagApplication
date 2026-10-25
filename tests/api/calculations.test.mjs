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
  assert.equal(result.slagOut, result.balSlagMass, 'both documented aliases map to MassOfOxSlagSum');
  assert.notEqual(result.caOBalSlagMass, result.balSlagMass,
    'the CaO balance uses UdWeight and differs on this integration fixture');
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

test('castIronTemp echoes 1450 and 1500 while temperature-independent outputs remain stable', async () => {
  const results = [];
  for (const temp of [1450, 1500]) {
    const variables = example('CalculateSlagMode');
    variables.input.iron.temp = temp;
    const result = (await operation('CalculateSlagMode', variables)).calculateSlagMode;
    assert.equal(result.castIronTemp, temp);
    assert.ok(Number.isFinite(result.slagTemperature));
    results.push(result);
  }
  assert.notEqual(results[0].slagTemperature, results[1].slagTemperature,
    'slagTemperature keeps the DLL temperature calculation');
  for (const key of ['totalMat', 'materialParts', 'slagBasicity2', 'slagBasicity3',
    'viscosity1400', 'viscosity1450', 'viscosity1500', 'viscosity1550']) {
    assert.deepEqual(results[0][key], results[1][key], key);
  }
});

test('calculation validation rejects individual negative consumption and invalid percentages', async (t) => {
  const cases = [
    ['CalculateSlagMode', i => { i.components[0].consumption = -1; }],
    ['CalculateSlagMode', i => { i.coke.consumption = -1; }],
    ['CalculateSlagMode', i => { i.slag.siO2 = 0; }],
    ['CalculateSlagMode', i => { i.iron.s = 100.01; }],
    ['CalculateSlagMode', i => { i.iron.temp = -273.15; }],
    ['CalculateSlagMode', i => { i.components[0].fe = -0.01; }],
  ];
  for (const [index, [name, mutate]] of cases.entries()) {
    await t.test(`${name} invalid input ${index}`, async () => {
      const variables = example(name);
      mutate(variables.input);
      expectErrors(await graphql(document(name), variables), { code: 'VALIDATION_FAILED' });
    });
  }
});

test('individual zero consumption remains legal when the total is positive', async () => {
  const variables = example('CalculateSlagMode');
  variables.input.components[0].consumption = 0;
  const result = (await operation('CalculateSlagMode', variables)).calculateSlagMode;
  assertFiniteFields(result, SLAG_NUMERIC_FIELDS, 'calculateSlagMode');
  assertMaterialParts(result, variables.input.components);
});

test('factual slag sulfur reaches the DLL distribution calculation with a compatible zero default', async () => {
  const results = [];
  for (const [slagSulfur, expected] of [[undefined, 0], [0.16, 10], [0.32, 20]]) {
    const variables = example('CalculateSlagMode');
    variables.input.iron.s = 0.016;
    if (slagSulfur !== undefined) variables.input.slag.s = slagSulfur;
    const result = (await operation('CalculateSlagMode', variables)).calculateSlagMode;
    assertFiniteFields(result, SLAG_NUMERIC_FIELDS, 'calculateSlagMode');
    assert.equal(result.sDistribution, expected);
    results.push(result);
  }
  for (const key of ['totalMat', 'materialParts', 'castIronTemp', 'slagBasicity2',
    'slagBasicity3', 'viscosity1400', 'viscosity1450', 'viscosity1500', 'viscosity1550']) {
    assert.deepEqual(results[0][key], results[1][key], key);
    assert.deepEqual(results[1][key], results[2][key], key);
  }
});

test('required nested calculation objects and nonnumeric values fail GraphQL coercion', async () => {
  for (const [name, mutate] of [
    ['CalculateSlagMode', i => { i.iron = null; }],
    ['CalculateSlagMode', i => { delete i.coke; }],
    ['CalculateSlagMode', i => { i.iron.temp = 'NaN'; }],
  ]) {
    const variables = example(name);
    mutate(variables.input);
    const response = await graphql(document(name), variables);
    expectErrors(response);
    assert.equal(response.body.data, undefined);
  }
});
