<#
.SYNOPSIS
Отправляет одну операцию из operations с переменными JSON. Совместим с PowerShell 5.1+.
.EXAMPLE
.\requests\Invoke-GraphQL.ps1 -Operation Hello
.EXAMPLE
.\requests\Invoke-GraphQL.ps1 -Operation CalcVariantById -VariablesPath .\requests\examples\CalcVariantById.variables.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Hello', 'BlastFurnaces', 'ComponentGuides', 'CalcVariants', 'CalcVariantById',
        'CalculateAglom', 'CalculateSlagMode', 'AddBlastFurnace', 'UpdateBlastFurnace',
        'DeleteBlastFurnace', 'AddComponentGuide', 'UpdateComponentGuide', 'DeleteComponentGuide',
        'AddCalcVariant', 'UpdateCalcVariant', 'DeleteCalcVariant', 'Login', 'Logout', 'ChangePassword')]
    [string]$Operation,
    [string]$VariablesPath,
    [object]$Variables,
    [uri]$Endpoint = 'http://localhost:5000/graphql',
    [hashtable]$Headers = @{},
    [string]$AccessToken,
    [ValidateRange(1, 600)]
    [int]$TimeoutSec = 60
)

$ErrorActionPreference = 'Stop'
if ($AccessToken) { $Headers['Authorization'] = "Bearer $AccessToken" }
$operationPath = Join-Path $PSScriptRoot "operations/$Operation.graphql"
if (-not $VariablesPath -and -not $PSBoundParameters.ContainsKey('Variables')) {
    $VariablesPath = Join-Path $PSScriptRoot "examples/$Operation.variables.json"
}

$query = Get-Content -LiteralPath $operationPath -Raw -Encoding UTF8
if (-not $PSBoundParameters.ContainsKey('Variables')) {
    $Variables = Get-Content -LiteralPath $VariablesPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
$body = @{
    operationName = $Operation
    query = $query
    variables = $variables
} | ConvertTo-Json -Depth 100

$response = Invoke-RestMethod -Uri $Endpoint -Method Post -Headers $Headers `
    -ContentType 'application/json; charset=utf-8' `
    -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec $TimeoutSec

if ($response.errors) {
    $codes = @($response.errors | ForEach-Object { if ($_.extensions.code) { $_.extensions.code } else { 'GRAPHQL_ERROR' } })
    throw ("GraphQL вернул errors: " + ($codes -join ', '))
}

$response
