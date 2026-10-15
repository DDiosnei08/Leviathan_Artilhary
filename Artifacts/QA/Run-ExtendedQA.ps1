$ErrorActionPreference='Stop'
$source='C:\ProjetoENTEC\Leviathan2'
$stage='C:\ProjetoENTEC\MapCompositionWork'
$output=Join-Path $source 'Artifacts/QA'
$unity='C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe'
# Restore the delivered assets after fixture-based validators before building.
foreach ($area in 'Assets','Packages','ProjectSettings') {
    & robocopy (Join-Path $source $area) (Join-Path $stage $area) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Snapshot copy failed: $area" }
}
Copy-Item -LiteralPath (Join-Path $output 'QaPlayerSmokeProbe.cs') -Destination (Join-Path $stage 'Assets/Scripts/Diagnostics/QaPlayerSmokeProbe.cs') -Force
$results=@()
foreach ($suite in @(
    @{Name='SceneryRetest';Method='AssetCurationValidation.ValidateBatch';Report='Artifacts/AssetCuration/Validation.txt'},
    @{Name='EnvironmentRetest';Method='EnvironmentEnrichmentValidation.ValidateBatch';Report='Artifacts/EnvironmentEnrichment/Validation.txt'},
    @{Name='Seeds';Method='ProjectQaValidation.SeedBatch';Report='Artifacts/QA/Seeds.txt'},
    @{Name='Tuning';Method='ProjectQaValidation.TuningBatch';Report='Artifacts/QA/Tuning.txt'},
    @{Name='Build';Method='ProjectQaValidation.BuildBatch';Report='Artifacts/QA/Build.txt'}
)) {
    if ($suite.Name -eq 'Build') {
        foreach ($area in 'Assets','Packages','ProjectSettings') {
            & robocopy (Join-Path $source $area) (Join-Path $stage $area) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
            if ($LASTEXITCODE -ge 8) { throw "Pre-build snapshot copy failed: $area" }
        }
    }
    $start=Get-Date
    Write-Output "START $($suite.Name)"
    $log=Join-Path $output ($suite.Name+'.log')
    $arguments="-batchmode -force-d3d11 -projectPath `"$stage`" -executeMethod $($suite.Method) -logFile `"$log`""
    $process=Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(1200000)) { $process.Kill();$process.WaitForExit();$code=124 } else {$code=$process.ExitCode}
    $report=Join-Path $stage $suite.Report
    $passed=0;$failed=0;$fresh=$false
    if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTime -ge $start.AddSeconds(-2)) {
        Copy-Item -LiteralPath $report -Destination (Join-Path $output ($suite.Name+'.txt')) -Force
        $lines=Get-Content -LiteralPath $report
        $passed=@($lines | Where-Object {$_ -match '^PASS '}).Count
        $failed=@($lines | Where-Object {$_ -match '^FAIL '}).Count
        $fresh=$true
    }
    $result=[pscustomobject]@{Suite=$suite.Name;Exit=$code;Passed=$passed;Failed=$failed;FreshReport=$fresh;Seconds=[math]::Round(((Get-Date)-$start).TotalSeconds,1)}
    $results+=$result
    $results | Export-Csv -LiteralPath (Join-Path $output 'ExtendedResults.csv') -NoTypeInformation
    Write-Output ($result | ConvertTo-Json -Compress)
}
if ($results[-1].Exit -eq 0) {
    $standalone=Join-Path $output 'Standalone'
    New-Item -ItemType Directory -Path $standalone -Force | Out-Null
    $exe=Join-Path $stage 'Artifacts/QA/Build/LeviathanQA.exe'
    $arguments="-batchmode -force-d3d11 -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -leviathan-qa-output `"$standalone`" -logFile `"$standalone\Player.log`""
    $process=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(300000)) {$process.Kill();$process.WaitForExit();$code=124} else {$code=$process.ExitCode}
    Write-Output "STANDALONE_EXIT=$code"
    "EXIT=$code" | Out-File -LiteralPath (Join-Path $standalone 'Exit.txt') -Encoding utf8
}
