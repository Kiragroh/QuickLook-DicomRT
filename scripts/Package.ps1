$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    dotnet build Plugin -c Release --nologo -v quiet
    if($LASTEXITCODE -ne 0){throw 'Plugin build failed'}
    $stage=Join-Path $projectRoot ('artifacts\package-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $assets=Get-Content 'Plugin\obj\project.assets.json' -Raw | ConvertFrom-Json
    $runtime=@($assets.targets.PSObject.Properties.Value.PSObject.Properties | ForEach-Object {$_.Value.runtime.PSObject.Properties.Name} | Where-Object {$_ -and $_.EndsWith('.dll')} | ForEach-Object {[IO.Path]::GetFileName($_)})
    $runtime+=@('QuickLook.Plugin.DicomRT.dll','QuickLook.Plugin.Metadata.config')
    foreach($name in ($runtime | Sort-Object -Unique)){if($name -eq 'QuickLook.Common.dll'){continue};Copy-Item -LiteralPath (Join-Path 'Plugin\bin\Release\net462' $name) -Destination $stage}
    Get-ChildItem -LiteralPath 'licenses' -Filter '*.txt' -File | Copy-Item -Destination $stage
    Copy-Item README.md,THIRD_PARTY.md -Destination $stage
    Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses\fo-dicom-MS-PL.html') -Destination (Join-Path $stage 'fo-dicom-MS-PL.html')
    $files=Get-ChildItem -LiteralPath $stage -File | ForEach-Object {[ordered]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length}}
    [ordered]@{version='0.2.18';builtAt=(Get-Date -Format o);files=@($files);clinicalApproval=$false} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=Join-Path $projectRoot 'artifacts\QuickLook.Plugin.DicomRT.qlplugin'
    if(Test-Path -LiteralPath $archive){$archive=Join-Path $projectRoot ('artifacts\QuickLook.Plugin.DicomRT.'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.qlplugin')}
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage,$archive)
    Get-FileHash -LiteralPath $archive -Algorithm SHA256
} finally {Pop-Location}
