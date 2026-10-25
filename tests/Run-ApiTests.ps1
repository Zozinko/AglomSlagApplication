[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$testProject = 'aglom-api-tests-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$composeFile = Join-Path $PSScriptRoot 'compose.api-tests.yml'
$previousUrl = $env:GRAPHQL_TEST_URL
$testExitCode = 1

function Invoke-TestCompose {
    param([string[]]$ComposeArguments)
    & docker compose -p $testProject -f $composeFile @ComposeArguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed: $ComposeArguments (exit $LASTEXITCODE)" }
}

try {
    & node --version
    if ($LASTEXITCODE -ne 0) { throw 'Node.js 22 or newer is required.' }
    Invoke-TestCompose -ComposeArguments @('up', '-d', '--build')
    $publishedPort = Invoke-TestCompose -ComposeArguments @('port', 'api', '8080')
    if ($publishedPort -notmatch '^127\.0\.0\.1:\d+$') { throw "Unexpected test API address: $publishedPort" }
    $env:GRAPHQL_TEST_URL = "http://$publishedPort/graphql"
    Write-Host "Isolated test API: $env:GRAPHQL_TEST_URL"
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $result = Invoke-RestMethod -Uri $env:GRAPHQL_TEST_URL -Method Post -ContentType 'application/json' -Body '{"query":"{ hello }"}' -TimeoutSec 2
            if ($result.data.hello -eq 'Aglom GraphQL API') { $ready = $true; break }
        } catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'The isolated test API did not become ready.' }
    $testFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'api') -Filter '*.test.mjs' | ForEach-Object FullName)
    if ($testFiles.Count -eq 0) { throw 'No API test files found.' }
    & node --test --test-reporter=spec @testFiles
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -ne 0) {
        Invoke-TestCompose -ComposeArguments @('logs', '--tail', '100', 'api', 'migrator')
    }
} catch {
    Write-Host $_ -ForegroundColor Red
    & docker compose -p $testProject -f $composeFile logs --tail 100
    $testExitCode = 1
} finally {
    $env:GRAPHQL_TEST_URL = $previousUrl
    & docker compose -p $testProject -f $composeFile down --volumes --remove-orphans --rmi local
    if ($LASTEXITCODE -ne 0) { $testExitCode = 1 }
}
exit $testExitCode
