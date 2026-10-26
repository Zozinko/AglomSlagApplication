[CmdletBinding()]
param([switch]$UpdateSchema)
$ErrorActionPreference = 'Stop'
$logDirectory = Join-Path $PSScriptRoot '../logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Start-Transcript -LiteralPath (Join-Path $logDirectory 'last-run.log') -Force | Out-Null
$testProject = 'aglom-api-tests-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
$composeFile = Join-Path $PSScriptRoot 'compose.api-tests.yml'
$testDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../tasks/SEC-02/local/$testProject"))
$keys = @('GRAPHQL_TEST_URL','API_TEST_COMPOSE_PROJECT','API_TEST_COMPOSE_FILE','API_TEST_DB_PASSWORD','API_TEST_JWT_KEY','API_TEST_USER_EMAIL','API_TEST_USER_PASSWORD','API_TEST_ADMIN_EMAIL','API_TEST_ADMIN_PASSWORD','API_TEST_ACCESS_TOKEN','API_TEST_SCHEMA_PATH')
$saved = @{}
foreach ($key in $keys) { $saved[$key] = [Environment]::GetEnvironmentVariable($key) }
$env:API_TEST_DB_PASSWORD = [guid]::NewGuid().ToString('N') + '$#; with spaces "quotes"'
$env:API_TEST_JWT_KEY = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:API_TEST_USER_EMAIL = 'user@example.invalid'
$env:API_TEST_ADMIN_EMAIL = 'admin@example.invalid'
$env:API_TEST_USER_PASSWORD = [guid]::NewGuid().ToString('N')
$env:API_TEST_ADMIN_PASSWORD = [guid]::NewGuid().ToString('N')
$env:API_TEST_COMPOSE_PROJECT = $testProject
$env:API_TEST_COMPOSE_FILE = $composeFile
$env:API_TEST_SCHEMA_PATH = Join-Path $testDirectory 'schema.graphql'
$contextProbe = Join-Path $PSScriptRoot "../Soft/AglomSlagServer/.env.$testProject"
$testExitCode = 1
function Invoke-TestCompose {
    param([string[]]$ComposeArguments)
    & docker compose -p $testProject -f $composeFile @ComposeArguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed (exit $LASTEXITCODE)." }
}
function Set-TestAccess {
    $publishedPort = (Invoke-TestCompose -ComposeArguments @('port','api','8080')).Trim()
    $env:GRAPHQL_TEST_URL = "http://$publishedPort/graphql"
    $body = @{ query = 'mutation($input:LoginInput!){login(input:$input){accessToken}}'; variables = @{ input = @{ email = $env:API_TEST_ADMIN_EMAIL; password = $env:API_TEST_ADMIN_PASSWORD } } } | ConvertTo-Json -Depth 10
    $result = Invoke-RestMethod -Uri $env:GRAPHQL_TEST_URL -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 5
    if (-not $result.data.login.accessToken) { throw 'Test administrator login failed.' }
    $env:API_TEST_ACCESS_TOKEN = $result.data.login.accessToken
}
try {
    New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
    [IO.File]::WriteAllText($contextProbe, "DB_PASSWORD='" + $env:API_TEST_DB_PASSWORD + "'`n")
    & node --version
    if ($LASTEXITCODE -ne 0) { throw 'Node.js 22 or newer is required.' }
    Invoke-TestCompose -ComposeArguments @('build')
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    Invoke-TestCompose -ComposeArguments @('run','--rm','--no-deps','--volume', "${repositoryRoot}:/repo:ro",'--workdir','/repo','--entrypoint','dotnet','migrator','run','--project','tests/credentials/CredentialChecks.csproj','--configuration','Release','--artifacts-path','/tmp/credential-checks')
    Invoke-TestCompose -ComposeArguments @('up','-d')
    $publishedPort = (Invoke-TestCompose -ComposeArguments @('port','api','8080')).Trim()
    if ($publishedPort -notmatch '^127\.0\.0\.1:\d+$') { throw 'Unexpected test API address.' }
    $env:GRAPHQL_TEST_URL = "http://$publishedPort/graphql"
    Write-Host "Isolated HTTP test API: $env:GRAPHQL_TEST_URL"
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try { Set-TestAccess; $ready = $true; break } catch { }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw 'The isolated API did not become ready.' }
    $schemaResponse = Invoke-WebRequest -Uri ($env:GRAPHQL_TEST_URL + '?sdl') -UseBasicParsing -TimeoutSec 30
    $schemaText = if ($schemaResponse.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($schemaResponse.Content) } else { [string]$schemaResponse.Content }
    if ($schemaText -notmatch '^schema\s*\{') { throw 'SDL download failed.' }
    [IO.File]::WriteAllText($env:API_TEST_SCHEMA_PATH, $schemaText, [Text.UTF8Encoding]::new($false))
    if ($UpdateSchema) { [IO.File]::WriteAllText((Join-Path $PSScriptRoot '../requests/schema.graphql'), $schemaText, [Text.UTF8Encoding]::new($false)) }
    $testFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'api') -Filter '*.test.mjs' | ForEach-Object FullName)
    & node --test --test-concurrency=1 --test-reporter=spec @testFiles | Out-Host
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -eq 0) {
        Set-TestAccess
        $extra = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'infrastructure') -Filter '*.test.mjs' | Where-Object Name -NotIn @('secrets.test.mjs','runtime.test.mjs') | ForEach-Object FullName)
        foreach ($file in $extra) {
            if ($testExitCode -eq 0) {
                Set-TestAccess
                & node --test --test-reporter=spec $file | Out-Host
                $testExitCode = $LASTEXITCODE
            }
        }
    }
    foreach ($file in @('secrets.test.mjs','runtime.test.mjs')) {
        if ($testExitCode -eq 0) {
            Set-TestAccess
            & node --test --test-reporter=spec (Join-Path $PSScriptRoot "infrastructure/$file") | Out-Host
            $testExitCode = $LASTEXITCODE
        }
    }
    if ($testExitCode -ne 0) { Invoke-TestCompose -ComposeArguments @('logs','--tail','100','api','migrator') }
} catch {
    Write-Host $_ -ForegroundColor Red
    & docker compose -p $testProject -f $composeFile logs --tail 100
    $testExitCode = 1
} finally {
    & docker compose -p $testProject -f $composeFile down --volumes --remove-orphans --rmi local
    if ($LASTEXITCODE -ne 0) { $testExitCode = 1 }
    Remove-Item -LiteralPath $contextProbe -Force -ErrorAction SilentlyContinue
    $expected = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../tasks/SEC-02/local/$testProject"))
    if ($expected -eq $testDirectory -and (Test-Path -LiteralPath $testDirectory)) { Remove-Item -LiteralPath $testDirectory -Recurse -Force }
    foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key, $saved[$key]) }
    Stop-Transcript | Out-Null
}
exit $testExitCode
