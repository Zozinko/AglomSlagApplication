import test from 'node:test';
import assert from 'node:assert/strict';

import { document, example, expectErrors, graphql, operation } from './helpers.mjs';

const EXPECTED_ROOT_FIELDS = {
  query: [
    'hello', 'blastFurnaces', 'componentGuides', 'calcVariants', 'calcVariantById',
  ],
  mutation: [
    'calculateAglom', 'calculateSlagMode', 'addBlastFurnace', 'updateBlastFurnace',
    'deleteBlastFurnace', 'addComponentGuide', 'updateComponentGuide',
    'deleteComponentGuide', 'addCalcVariant', 'updateCalcVariant', 'deleteCalcVariant',
  ],
};

function parseRootFields(sdl, rootType) {
  const match = sdl.match(new RegExp(`\\btype\\s+${rootType}\\s*\\{([\\s\\S]*?)\\n\\}`));
  assert.ok(match, `SDL should declare type ${rootType}`);
  return [...match[1].matchAll(/^[ \t]{2}([_A-Za-z][_0-9A-Za-z]*)\s*(?:\(|:)/gm)]
    .map(([, field]) => field);
}

function assertGraphQLErrorsWithoutPath(response, label) {
  expectErrors(response);
  assert.ok(response.status === 200 || response.status === 400,
    `${label}: expected a GraphQL HTTP response, got ${response.status}`);
  assert.ok(response.body.errors.every((error) => error.path === undefined),
    `${label}: parse, validation, or input-coercion errors should not have resolver paths`);
}

test('GET /graphql?sdl exposes exactly the documented query and mutation fields', async () => {
  const endpoint = process.env.GRAPHQL_TEST_URL;
  assert.ok(endpoint, 'GRAPHQL_TEST_URL is required; no live endpoint is assumed');
  const url = new URL(endpoint);
  url.searchParams.set('sdl', '');
  const response = await fetch(url, { signal: AbortSignal.timeout(60_000) });
  assert.equal(response.status, 200, 'SDL endpoint should respond successfully');
  const sdl = await response.text();

  const actualQuery = parseRootFields(sdl, 'Query');
  const actualMutation = parseRootFields(sdl, 'Mutation');
  assert.deepEqual([...actualQuery].sort(), [...EXPECTED_ROOT_FIELDS.query].sort());
  assert.deepEqual([...actualMutation].sort(), [...EXPECTED_ROOT_FIELDS.mutation].sort());
  assert.equal(actualQuery.length, 5);
  assert.equal(actualMutation.length, 11);
});

test('hello query returns the documented readiness string', async () => {
  assert.equal((await operation('Hello')).hello, 'Aglom GraphQL API');
});

test('every root operation has a matching executable document and variables example', () => {
  for (const field of [...EXPECTED_ROOT_FIELDS.query, ...EXPECTED_ROOT_FIELDS.mutation]) {
    const operationName = field[0].toUpperCase() + field.slice(1);
    const source = document(operationName);
    assert.match(source, new RegExp(`\\b${field}\\b`), `${operationName} should select its root field`);
    const variables = example(operationName);
    assert.ok(variables && typeof variables === 'object' && !Array.isArray(variables),
      `${operationName} should have a JSON variables object`);
  }
});

test('GraphQL rejects syntax, unknown fields, missing required values, wrong types, and invalid enum values before execution', async (t) => {
  const cases = [
    ['malformed document', 'query Broken { hello(', {}],
    ['unknown field', 'query UnknownField { notARealField }', {}],
    [
      'missing required variable',
      'mutation Calculate($input: CalculateAglomInput!) { calculateAglom(input: $input) { id } }',
      {},
    ],
    [
      'missing required input fields',
      'mutation Calculate($input: CalculateAglomInput!) { calculateAglom(input: $input) { id } }',
      { input: {} },
    ],
    [
      'wrong scalar type',
      'mutation Calculate($input: CalculateAglomInput!) { calculateAglom(input: $input) { id } }',
      { input: { ...example('CalculateAglom').input, userId: 'not-an-integer' } },
    ],
    [
      'unknown enum value',
      'mutation Calculate($input: CalculateAglomInput!) { calculateAglom(input: $input) { id } }',
      { input: { ...example('CalculateAglom').input,
        aglomParameters: { ...example('CalculateAglom').input.aglomParameters, fluxType: 'NO_SUCH_FLUX' } } },
    ],
  ];

  for (const [label, query, variables] of cases) {
    await t.test(label, async () => {
      const response = await graphql(query, variables);
      assertGraphQLErrorsWithoutPath(response, label);
      assert.equal(response.body.data, undefined, `${label}: invalid requests must not produce data`);
    });
  }
});

test('Production GraphQL introspection is rejected with HC0046', async () => {
  const response = await graphql('{ __type(name: "Query") { name } }');
  assertGraphQLErrorsWithoutPath(response, 'introspection');
  assert.ok(response.body.errors.some(({ extensions }) => extensions?.code === 'HC0046'),
    'Production introspection should report Hot Chocolate error HC0046');
});
