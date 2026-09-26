$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    dotnet build Plugin -c Release --nologo -v quiet
    if($LASTEXITCODE -ne 0){throw 'Plugin build failed'}
    $stage=Join-Path $projectRoot ('artifacts\package-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Get-ChildItem 'Plugin\bin\Release\net462' -File | Where-Object {$_.Extension -eq '.dll' -or $_.Name -eq 'QuickLook.Plugin.Metadata.config'} | Copy-Item -Destination $stage
    Copy-Item README.md,THIRD_PARTY.md -Destination $stage
    Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses\fo-dicom-MS-PL.html') -Destination (Join-Path $stage 'fo-dicom-MS-PL.html')
    $files=Get-ChildItem -LiteralPath $stage -File | ForEach-Object {[ordered]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length}}
    [ordered]@{version='0.2.0';builtAt=(Get-Date -Format o);files=@($files);clinicalApproval=$false} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=Join-Path $projectRoot 'artifacts\QuickLook.Plugin.DicomRT.qlplugin'
    if(Test-Path -LiteralPath $archive){$archive=Join-Path $projectRoot ('artifacts\QuickLook.Plugin.DicomRT.'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.qlplugin')}
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage,$archive)
    Get-FileHash -LiteralPath $archive -Algorithm SHA256
} finally {Pop-Location}
