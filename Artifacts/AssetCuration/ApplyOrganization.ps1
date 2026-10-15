param([string]$ProjectRoot='C:\ProjetoENTEC\Leviathan2')
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path
$assets=[IO.Path]::GetFullPath((Join-Path $root 'Assets'))+[IO.Path]::DirectorySeparatorChar
$thirdParty=[IO.Path]::GetFullPath((Join-Path $root 'Assets/ThirdParty/Environment'))+[IO.Path]::DirectorySeparatorChar
$archive=[IO.Path]::GetFullPath((Join-Path $root 'Artifacts/AssetCuration/Unused'))+[IO.Path]::DirectorySeparatorChar
$manifest=Join-Path $root 'Artifacts/AssetCuration/Moves.tsv'
foreach($line in Get-Content -LiteralPath $manifest) {
    if(!$line) {continue}
    $parts=$line.Split("`t")
    if($parts.Length -ne 3) {throw 'Invalid manifest record'}
    $source=[IO.Path]::GetFullPath((Join-Path $root $parts[1]))
    $target=[IO.Path]::GetFullPath((Join-Path $root $parts[2]))
    $allowed=if($parts[0] -eq 'MOVE') {$thirdParty} elseif($parts[0] -eq 'ARCHIVE') {$archive} else {throw 'Invalid move kind'}
    if(!$source.StartsWith($assets,[StringComparison]::OrdinalIgnoreCase) -or !$target.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) {throw 'Move escaped its allowed directories'}
    if(!(Test-Path -LiteralPath $source)) {
        if(Test-Path -LiteralPath $target) {continue}
        throw "Source missing: $source"
    }
    if(Test-Path -LiteralPath $target) {throw "Destination already exists: $target"}
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Move-Item -LiteralPath $source -Destination $target
    if(Test-Path -LiteralPath "$source.meta") {Move-Item -LiteralPath "$source.meta" -Destination "$target.meta"}
}
$packages=@('Darth_Artisan','Free','Industrial Props','Sector Warzone','Defensive_props','Lowpoly_Crates','RSG','RockFREE','PBR_Rock_Cliffs_Pack','Fences','StreetLightsPack','NOT_Lonely','Viverna_Assets','AK STUDIO ART','PHOSdigital','AssetsStore','Vefects','Announcer','Military WW2 Music Pack','_TerrainAutoUpgrade')
foreach($package in $packages) {
    $folder=[IO.Path]::GetFullPath((Join-Path $root "Assets/$package"))
    if(!$folder.StartsWith($assets,[StringComparison]::OrdinalIgnoreCase)) {throw 'Invalid package cleanup target'}
    if(!(Test-Path -LiteralPath $folder)) {continue}
    $remaining=Get-ChildItem -LiteralPath $folder -File -Recurse
    if($remaining | Where-Object {$_.Extension -ne '.meta'}) {throw "Unexpected files remain in $folder"}
    foreach($file in $remaining) {
        $relative=$file.FullName.Substring($assets.Length)
        $target=[IO.Path]::GetFullPath((Join-Path $archive $relative))
        if(!$target.StartsWith($archive,[StringComparison]::OrdinalIgnoreCase)) {throw 'Invalid metadata archive target'}
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Move-Item -LiteralPath $file.FullName -Destination $target
    }
    if(Test-Path -LiteralPath "$folder.meta") {Move-Item -LiteralPath "$folder.meta" -Destination (Join-Path $archive "$package.meta")}
    Remove-Item -LiteralPath $folder -Recurse
}
Write-Output 'Downloaded assets organized with their metadata; unused content archived.'
