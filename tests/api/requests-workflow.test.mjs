import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

test('requests scripts emulate both users over HTTP: login, read, calculation, save, refresh and logout', () => {
  const script = `
$ErrorActionPreference = 'Stop'
$request = Join-Path $env:API_TEST_REQUESTS_DIRECTORY 'Invoke-GraphQL.ps1'
$endpoint = $env:GRAPHQL_TEST_URL
$user = (& $request -Operation Login -Endpoint $endpoint -Variables @{input=@{email=$env:API_TEST_USER_EMAIL;password=$env:API_TEST_USER_PASSWORD}}).data.login
$admin = (& $request -Operation Login -Endpoint $endpoint -Variables @{input=@{email=$env:API_TEST_ADMIN_EMAIL;password=$env:API_TEST_ADMIN_PASSWORD}}).data.login
$created = $null
try {
    $denied = $false
    try { $null = & $request -Operation Hello -Endpoint $endpoint }
    catch { $denied = $_.Exception.Message.Contains('AUTH_NOT_AUTHENTICATED') }
    $hello = (& $request -Operation Hello -Endpoint $endpoint -AccessToken $user.accessToken).data.hello
    $null = & $request -Operation CalculateSlagMode -Endpoint $endpoint -AccessToken $user.accessToken
    $variables = Get-Content (Join-Path $env:API_TEST_REQUESTS_DIRECTORY 'examples/AddCalcVariant.variables.json') -Raw | ConvertFrom-Json
    $variables.input.bfId = -1
    $variables.input.variantName = 'requests-workflow-' + [guid]::NewGuid().ToString('N')
    $variables.components = @()
    $created = (& $request -Operation AddCalcVariant -Endpoint $endpoint -AccessToken $user.accessToken -Variables $variables).data.addCalcVariant
    $loaded = (& $request -Operation CalcVariantById -Endpoint $endpoint -AccessToken $user.accessToken -Variables @{variantId=$created.variantId}).data.calcVariantById
    $adminRows = (& $request -Operation CalcVariants -Endpoint $endpoint -AccessToken $admin.accessToken).data.calcVariants
    $user = (& $request -Operation Login -Endpoint $endpoint -Variables @{input=@{refreshToken=$user.refreshToken}}).data.login
    $logout = (& $request -Operation Logout -Endpoint $endpoint -AccessToken $user.accessToken).data.logout
    @{anonymousDenied=$denied;hello=$hello;userId=$user.user.userId;admin=$admin.user.admin;roundTrip=($loaded.variantId -eq $created.variantId);adminSeesUser=(@($adminRows | Where-Object variantId -eq $created.variantId).Count -eq 1);logout=$logout} | ConvertTo-Json -Compress
} finally {
    if ($created) { $null = & $request -Operation DeleteCalcVariant -Endpoint $endpoint -AccessToken $user.accessToken -Variables @{variantId=$created.variantId} }
}
`;
  const result = execFileSync(process.env.API_TEST_POWERSHELL ?? 'pwsh', ['-NoProfile', '-Command', script], {
    encoding: 'utf8', timeout: 90_000,
    env: { ...process.env, API_TEST_REQUESTS_DIRECTORY: fileURLToPath(new URL('../../requests/', import.meta.url)) },
  });
  assert.deepEqual(JSON.parse(result), { anonymousDenied: true, hello: 'Aglom GraphQL API', userId: -1, admin: true, roundTrip: true, adminSeesUser: true, logout: true });
});
