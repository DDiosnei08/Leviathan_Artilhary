param([string]$Stage = 'C:\ProjetoENTEC\MapCompositionWork', [string]$Output = 'C:\ProjetoENTEC\Leviathan2\Artifacts\QA')
$ErrorActionPreference = 'Stop'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe'
$suites = @(
    @{ Name='AssetAudit'; Method='ProjectQaValidation.AuditBatch'; Report='Artifacts/QA/AssetAudit.txt' },
    @{ Name='ProjectSweep'; Method='ProjectSweepValidation.ValidateBatch'; Report='Artifacts/ProjectSweep/Validation.txt' },
    @{ Name='Base'; Method='BaseIntegrationValidation.ValidateBatch'; Report='Artifacts/BaseIntegration/PlayValidation.txt' },
    @{ Name='Map'; Method='MapCompositionValidation.ValidateBatch'; Report='Artifacts/MapComposition/MapValidation.txt' },
    @{ Name='Sky'; Method='LayeredSkyValidation.ValidateBatch'; Report='Artifacts/LayeredSky/Validation.txt' },
    @{ Name='DistanceFPS'; Method='RenderDistanceFpsValidation.ValidateBatch'; Report='Artifacts/RenderDistanceFps/Validation.txt' },
    @{ Name='GrassEdge'; Method='BaseGrassEdgeValidation.IntegratedBatch'; Report='Artifacts/GrassBaseEdge/IntegratedValidation.txt' },
    @{ Name='Scenery'; Method='AssetCurationValidation.ValidateBatch'; Report='Artifacts/AssetCuration/Validation.txt' },
    @{ Name='Environment'; Method='EnvironmentEnrichmentValidation.ValidateBatch'; Report='Artifacts/EnvironmentEnrichment/Validation.txt' },
    @{ Name='Materials'; Method='DieselpunkVisualValidation.ValidateBatch'; Report='Artifacts/DieselpunkPolish/Validation.txt' },
    @{ Name='GrassCulling'; Method='GrassValidation.Stage2Batch'; Report='' },
    @{ Name='Spline'; Method='SurfaceSplineValidation.ValidateBatch'; Report='Artifacts/MapComposition/SplineValidation.txt' }
)
$results = @()
foreach ($suite in $suites) {
    $start = Get-Date
    Write-Output "START $($suite.Name) $($start.ToString('o'))"
    $log = Join-Path $Output ($suite.Name + '.log')
    $arguments = "-batchmode -force-d3d11 -projectPath `"$Stage`" -executeMethod $($suite.Method) -logFile `"$log`""
    $process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $timeout = !$process.WaitForExit(600000)
    if ($timeout) { $process.Kill(); $process.WaitForExit(); $code=124 } else { $code=$process.ExitCode }
    $passed=0; $failed=0; $fresh=$false
    if ($suite.Report) {
        $report = Join-Path $Stage $suite.Report
        if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $start.AddSeconds(-2)) {
            $fresh=$true
            Copy-Item -LiteralPath $report -Destination (Join-Path $Output ($suite.Name+'.txt')) -Force
            $lines = Get-Content -LiteralPath $report
            $passed=@($lines | Where-Object { $_ -match '^PASS ' }).Count
            $failed=@($lines | Where-Object { $_ -match '^FAIL ' }).Count
        }
    }
    $result=[pscustomobject]@{ Suite=$suite.Name; Exit=$code; Passed=$passed; Failed=$failed; FreshReport=$fresh; Seconds=[math]::Round(((Get-Date)-$start).TotalSeconds,1); Method=$suite.Method }
    $results += $result
    $results | Export-Csv -LiteralPath (Join-Path $Output 'SuiteResults.csv') -NoTypeInformation
    Write-Output ($result | ConvertTo-Json -Compress)
}
