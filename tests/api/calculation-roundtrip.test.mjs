import test from 'node:test';
import assert from 'node:assert/strict';
import { document, example, fixture, graphql, operation } from './helpers.mjs';

const cokeFields = {
  consumption: 'cokeConsumption', sulfur: 'cokeSContent', ashAmount: 'cokeAshAmount',
  ashCaOFraction: 'cokeAshCaOContent', ashSiO2Fraction: 'cokeAshSiO2Content',
  ashAl2O3Fraction: 'cokeAshAl2O3Content', ashMgOFraction: 'cokeAshMgOContent',
};
const ironFields = { temp: 'ciTemperature', si: 'ciSiContent', s: 'ciSContent',
  mn: 'ciMnContent', c: 'ciCContent', ti: 'ciTiContent', cr: 'ciCrContent' };
const slagFields = { caO: 'slagCaOContent', siO2: 'slagSiO2Content',
  tiO2: 'slagTiO2Content', al2O3: 'slagAl2O3Content', mgO: 'slagMgOContent', s: 'slagSContent' };
const materialFields = { fe: 'feContent', siO2: 'siO2Content', al2O3: 'al2O3Content',
  caO: 'caOContent', mgO: 'mgOContent', s: 'sContent', mnO: 'mnOContent', tiO2: 'tiO2Content' };
const encode = (source, fields) => Object.fromEntries(Object.entries(fields).map(([from, to]) => [to, source[from]]));
const decode = (source, fields) => Object.fromEntries(Object.entries(fields).map(([to, from]) => [to, source[from]]));

// IEEE-754 binary32 normal numbers round with relative error at most 2^-24.
// This fixture has no nonzero subnormals. Allow binary64 JSON arithmetic as well.
const floatStorageBound = value => Math.abs(value) * 2 ** -24 + Number.EPSILON;
function assertStoragePrecision(original, restored) {
  for (const [key, value] of Object.entries(original)) {
    if (typeof value === 'number') {
      assert.ok(Math.abs(restored[key] - value) <= floatStorageBound(value),
        `${key}: binary32 storage bound exceeded: ${value} -> ${restored[key]}`);
    }
  }
}

for (const mode of ['binary-exact', 'decimal']) {
  test(`slag calculation survives variant storage round-trip (${mode})`, async t => {
    const f = fixture(t);
    const source = example('CalculateSlagMode').input;
    source.slag.s = 0.16;
    if (mode === 'binary-exact') {
      // Preserve a positive sulfur concentration while making every number exactly
      // representable by both the storage float and calculator double.
      for (const object of [source.coke, source.iron, source.slag, ...source.components]) {
        for (const key of Object.keys(object)) {
          if (typeof object[key] === 'number') object[key] = Math.round(object[key] * 128) / 128;
        }
      }
    }
    const original = (await operation('CalculateSlagMode', { input: source })).calculateSlagMode;
    assert.ok(Math.abs(original.sDistribution - source.slag.s / source.iron.s) <= Number.EPSILON * 32,
      'factual sulfur distribution matches the verified public DLL example');
    const furnace = await f.furnace();
    const components = [];
    for (const material of source.components) {
      const guide = await f.guide({ componentName: material.sourcename, ...encode(material, materialFields) });
      components.push({ componentId: guide.componentId, componentName: material.sourcename,
        consumption: material.consumption, ...encode(material, materialFields) });
    }
    const variant = await f.variant(furnace.bfId, components, {
      ...encode(source.coke, cokeFields), ...encode(source.iron, ironFields), ...encode(source.slag, slagFields),
    });
    const readResponse = await graphql(document('CalcVariantById').replace('slagCaOContent',
      'slagSContent slagCaOContent'), { variantId: variant.variantId });
    assert.equal(readResponse.body.errors, undefined, JSON.stringify(readResponse.body.errors));
    const saved = readResponse.body.data.calcVariantById;
    const restored = {
      coke: decode(saved, cokeFields), iron: decode(saved, ironFields), slag: decode(saved, slagFields),
      components: source.components.map(material => {
        const savedMaterial = saved.componentVariants.find(row => row.componentName === material.sourcename);
        assert.ok(savedMaterial, `stored material ${material.sourcename}`);
        return { sourcename: savedMaterial.componentName, consumption: savedMaterial.consumption,
          ...decode(savedMaterial, materialFields) };
      }),
    };
    for (const group of ['coke', 'iron', 'slag']) assertStoragePrecision(source[group], restored[group]);
    source.components.forEach((material, index) => assertStoragePrecision(material, restored.components[index]));
    const result = (await operation('CalculateSlagMode', { input: restored })).calculateSlagMode;
    for (const [key, value] of Object.entries(result)) {
      if (typeof value === 'number') assert.ok(Number.isFinite(value), `${key} must remain finite`);
    }
    if (mode === 'binary-exact') {
      assert.deepEqual(result, original, 'lossless storage must preserve the complete calculation');
    } else {
      const totalBound = source.components.reduce((sum, row) => sum + floatStorageBound(row.consumption), 0);
      assert.ok(Math.abs(result.totalMat - original.totalMat) <= totalBound + Number.EPSILON * original.totalMat);
      assert.ok(Math.abs(result.castIronTemp - original.castIronTemp) <= floatStorageBound(source.iron.temp));
      const unitRoundoff = 2 ** -24;
      // For positive a,b each rounded by at most u, the ratio changes by at
      // most |a/b| * 2u/(1-u). This bound follows from storage, not DLL fitting.
      const ratioBound = Math.abs(original.sDistribution) * 2 * unitRoundoff / (1 - unitRoundoff) + Number.EPSILON * 32;
      assert.ok(Math.abs(result.sDistribution - original.sDistribution) <= ratioBound,
        'sulfur-distribution ratio must stay within the derived binary32 bound');
      // No numerical tolerance for opaque nonlinear DLL formulas can be inferred
      // from binary32 input accuracy alone; report observed differences as evidence.
      t.diagnostic(JSON.stringify(Object.fromEntries(Object.keys(result)
        .filter(key => typeof result[key] === 'number')
        .map(key => [key, { before: original[key], after: result[key], delta: result[key] - original[key] }]))));
    }
    // Adapter-source regression, not a metallurgy golden: both names map to
    // SlagMode.MassOfOxSlagSum; the domain meaning still needs the DLL contract.
    assert.equal(result.slagOut, result.balSlagMass);
  });
}
