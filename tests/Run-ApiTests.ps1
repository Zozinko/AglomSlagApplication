[CmdletBinding()]
param([switch]$UpdateSchema)

$ErrorActionPreference = 'Stop'
$testProject = 'aglom-api-tests-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$composeFile = Join-Path $PSScriptRoot 'compose.api-tests.yml'
$previousUrl = $env:GRAPHQL_TEST_URL
$previousProject = $env:API_TEST_COMPOSE_PROJECT
$previousCompose = $env:API_TEST_COMPOSE_FILE
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
    $apiContainer = Invoke-TestCompose -ComposeArguments @('ps', '-a', '-q', 'api')
    Write-Host "Isolated test API: $env:GRAPHQL_TEST_URL"
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try {
            $result = Invoke-RestMethod -Uri $env:GRAPHQL_TEST_URL -Method Post -ContentType 'application/json' -Body '{"query":"{ hello }"}' -TimeoutSec 2
            if ($result.data.hello -eq 'Aglom GraphQL API') { $ready = $true; break }
        } catch { }
        $apiState = & docker inspect --format '{{.State.Status}}' $apiContainer
        if ($LASTEXITCODE -ne 0 -or $apiState -in @('exited', 'dead')) {
            throw 'The isolated API exited before readiness; see container logs below.'
        }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'The isolated test API did not become ready.' }
    if ($UpdateSchema) {
        $schema = Invoke-WebRequest -Uri ($env:GRAPHQL_TEST_URL + '?sdl') -TimeoutSec 30
        $schemaPath = Join-Path $PSScriptRoot '../requests/schema.graphql'
        $schemaText = if ($schema.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($schema.Content) } else { [string]$schema.Content }
        if ($schemaText -notmatch '^schema\s*\{') { throw 'The SDL download did not contain a GraphQL schema.' }
        [System.IO.File]::WriteAllText($schemaPath, $schemaText, [System.Text.UTF8Encoding]::new($false))
    }
    $testFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'api') -Filter '*.test.mjs' | ForEach-Object FullName)
    if ($testFiles.Count -eq 0) { throw 'No API test files found.' }
    & node --test --test-reporter=spec @testFiles
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -eq 0) {
        $env:API_TEST_COMPOSE_PROJECT = $testProject
        $env:API_TEST_COMPOSE_FILE = $composeFile
        & node --test --test-reporter=spec (Join-Path $PSScriptRoot 'infrastructure/runtime.test.mjs')
        $testExitCode = $LASTEXITCODE
    }
    if ($testExitCode -ne 0) {
        Invoke-TestCompose -ComposeArguments @('logs', '--tail', '100', 'api', 'migrator')
    }
} catch {
    Write-Host $_ -ForegroundColor Red
    & docker compose -p $testProject -f $composeFile logs --tail 100
    $testExitCode = 1
} finally {
    $env:GRAPHQL_TEST_URL = $previousUrl
    $env:API_TEST_COMPOSE_PROJECT = $previousProject
    $env:API_TEST_COMPOSE_FILE = $previousCompose
    & docker compose -p $testProject -f $composeFile down --volumes --remove-orphans --rmi local
    if ($LASTEXITCODE -ne 0) { $testExitCode = 1 }
}
exit $testExitCode
