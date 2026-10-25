import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';

const endpoint = () => process.env.GRAPHQL_TEST_URL;
assert.ok(endpoint(), 'Set GRAPHQL_TEST_URL to an isolated test API; use tests/Run-ApiTests.ps1.');
const requests = new URL('../../requests/', import.meta.url);

export const document = name => readFileSync(new URL(`operations/${name}.graphql`, requests), 'utf8');
export const example = name => JSON.parse(readFileSync(new URL(`examples/${name}.variables.json`, requests), 'utf8'));

export async function graphql(query, variables = {}) {
  const response = await fetch(endpoint(), {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ query, variables }),
    signal: AbortSignal.timeout(60_000),
  });
  return { status: response.status, body: await response.json() };
}

export async function operation(name, variables = example(name)) {
  const response = await graphql(document(name), variables);
  assert.equal(response.status, 200, `${name}: ${JSON.stringify(response.body)}`);
  assert.equal(response.body.errors, undefined, `${name}: ${JSON.stringify(response.body.errors)}`);
  assert.ok(response.body.data, `${name}: no data`);
  return response.body.data;
}

export function expectErrors(response, { field, messagePattern, code } = {}) {
  assert.ok([200, 400].includes(response.status), `Unexpected HTTP status ${response.status}`);
  assert.ok(response.body.errors?.length, `Expected GraphQL errors: ${JSON.stringify(response.body)}`);
  if (field) assert.ok(response.body.errors.some(error => error.path?.includes(field)), JSON.stringify(response.body));
  if (messagePattern) assert.match(JSON.stringify(response.body.errors), messagePattern);
  if (code) assert.ok(response.body.errors.some(error => error.extensions?.code === code), JSON.stringify(response.body));
  return response.body.errors;
}

export function componentInput(guide, overrides = {}) {
  const { componentId, componentName, feContent, siO2Content, al2O3Content, caOContent, mgOContent, sContent, mnOContent, tiO2Content } = guide;
  return { componentId, componentName, consumption: 1653.4, feContent, siO2Content, al2O3Content, caOContent, mgOContent, sContent, mnOContent, tiO2Content, ...overrides };
}

export function fixture(t) {
  const ids = { variants: [], guides: [], furnaces: [] };
  const name = () => `api-test-${randomUUID()}`;
  t.after(async () => {
    // Try every cleanup even if one fails, and report all failures to the runner.
    const failures = [];
    for (const [kind, op, field] of [
      ['variants', 'DeleteCalcVariant', 'variantId'],
      ['guides', 'DeleteComponentGuide', 'componentId'],
      ['furnaces', 'DeleteBlastFurnace', 'bfId'],
    ]) {
      for (const id of ids[kind].toReversed()) {
        try { await operation(op, { [field]: id }); } catch (error) { failures.push(error); }
      }
    }
    if (failures.length) throw new AggregateError(failures, 'Test data cleanup failed');
  });
  return {
    async furnace(bfName = name()) {
      const { addBlastFurnace } = await operation('AddBlastFurnace', { bfName });
      ids.furnaces.push(addBlastFurnace.bfId);
      return addBlastFurnace;
    },
    async guide(overrides = {}) {
      const input = { ...example('AddComponentGuide').input, componentName: name(), ...overrides };
      const { addComponentGuide } = await operation('AddComponentGuide', { input });
      ids.guides.push(addComponentGuide.componentId);
      return addComponentGuide;
    },
    async variant(bfId, components = [], overrides = {}) {
      const input = { ...example('AddCalcVariant').input, bfId, variantName: name(), ...overrides };
      const { addCalcVariant } = await operation('AddCalcVariant', { input, components });
      ids.variants.push(addCalcVariant.variantId);
      return addCalcVariant;
    },
  };
}
