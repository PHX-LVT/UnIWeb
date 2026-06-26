param(
    [string]$MongoUri = "mongodb://localhost:27017/FullProjectDbVersion2?replicaSet=rs0",
    [string]$ApiBaseUrl = "https://localhost:6969",
    [switch]$SkipApi
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$reportsDir = Join-Path $scriptDir "reports"
New-Item -ItemType Directory -Force -Path $reportsDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$rawAuditPath = Join-Path $reportsDir "phase0-audit-$timestamp.raw.txt"
$jsonAuditPath = Join-Path $reportsDir "phase0-audit-$timestamp.json"

Write-Host "Running Mongo schema audit..."
$auditScript = Join-Path $scriptDir "audit-db.js"
& mongosh $MongoUri --quiet $auditScript | Set-Content -Path $rawAuditPath

$raw = Get-Content -Path $rawAuditPath -Raw
$jsonStart = $raw.IndexOf("{")
if ($jsonStart -lt 0) {
    throw "Mongo audit did not return JSON. See $rawAuditPath"
}

$raw.Substring($jsonStart) | Set-Content -Path $jsonAuditPath
$report = Get-Content -Path $jsonAuditPath -Raw | ConvertFrom-Json

$missingPairs = 0
foreach ($pair in $report.pairHealth) {
    $missingPairs += @($pair.missingDraft).Count
    $missingPairs += @($pair.missingPublished).Count
}

$summary = [pscustomobject]@{
    GeneratedAt = $report.generatedAt
    Collections = @($report.collections).Count
    SchemaDrift = @($report.schemaDrift).Count
    DuplicateStableIds = @($report.duplicateStableIds).Count
    MissingDraftOrPublishedPairs = $missingPairs
    StalePublishedContent = @($report.stalePublishedContent).Count
    LegacyBase64Fields = @($report.base64Usage).Count
    ReportPath = $jsonAuditPath
}

$summary | Format-List

if (-not $SkipApi) {
    Write-Host "Running public API smoke probes..."

    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

        $endpoints = @(
            "/api/public/navigation",
            "/api/public/theme",
            "/api/public/branding",
            "/api/public/global-buttons",
            "/api/public/pages/home",
            "/api/public/pages/solutions",
            "/api/public/pages/industry",
            "/api/public/pages/technology",
            "/api/public/pages/sustainability",
            "/api/public/pages/insights"
        )

        $apiResults = foreach ($endpoint in $endpoints) {
            $url = "$ApiBaseUrl$endpoint"
            try {
                $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
                [pscustomobject]@{ Endpoint = $endpoint; StatusCode = [int]$response.StatusCode; Ok = $true }
            }
            catch {
                [pscustomobject]@{ Endpoint = $endpoint; StatusCode = $null; Ok = $false; Error = $_.Exception.Message }
            }
        }

        $apiResults | Format-Table -AutoSize
    }
    catch {
        Write-Warning "API smoke probes skipped or failed to initialize: $($_.Exception.Message)"
    }
}

if ($summary.SchemaDrift -gt 0 -or $summary.DuplicateStableIds -gt 0 -or $summary.MissingDraftOrPublishedPairs -gt 0 -or $summary.LegacyBase64Fields -gt 0) {
    throw "Phase 0 hard checks failed. See $jsonAuditPath"
}

Write-Host "Phase 0 hard checks passed."
