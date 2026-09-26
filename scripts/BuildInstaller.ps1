param([string]$PackagePath,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if([string]::IsNullOrWhiteSpace($PackagePath)) {
    $package=Get-ChildItem -LiteralPath (Join-Path $repoRoot 'artifacts') -Filter '*.qlplugin' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if($null -eq $package){throw 'Run scripts/Package.ps1 first.'}
    $PackagePath=$package.FullName
}
$PackagePath=(Resolve-Path -LiteralPath $PackagePath).Path
if([string]::IsNullOrWhiteSpace($OutputDirectory)){$OutputDirectory=Join-Path $repoRoot 'artifacts\release'}
$buildArtifacts=Join-Path $repoRoot 'artifacts\installer'
New-Item -ItemType Directory -Force -Path $buildArtifacts,$OutputDirectory | Out-Null
$hash=(Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
$hashPath=Join-Path $buildArtifacts 'payload.sha256'
Set-Content -LiteralPath $hashPath -Value $hash -Encoding ascii
dotnet build (Join-Path $repoRoot 'Installer.Tests\Installer.Tests.csproj') -c Release --nologo -v quiet
if($LASTEXITCODE -ne 0){throw 'Installer test build failed.'}
& (Join-Path $repoRoot 'Installer.Tests\bin\Release\net462\Installer.Tests.exe') $PackagePath
if($LASTEXITCODE -ne 0){throw 'Installer payload tests failed.'}
dotnet build (Join-Path $repoRoot 'Installer\Installer.csproj') -c Release --nologo -v quiet "-p:PayloadPath=$PackagePath" "-p:PayloadHashPath=$hashPath"
if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
$target=Join-Path $OutputDirectory 'QuickLook-DicomRT-Setup-0.2.4.exe'
Copy-Item -LiteralPath (Join-Path $repoRoot 'Installer\bin\Release\net462\QuickLook-DicomRT-Setup.exe') -Destination $target -Force
$stdout=Join-Path $buildArtifacts 'verify-payload.stdout.txt'
$stderr=Join-Path $buildArtifacts 'verify-payload.stderr.txt'
$verification=Start-Process -FilePath $target -ArgumentList '--verify-payload' -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if($verification.ExitCode -ne 0){Get-Content -LiteralPath $stderr;throw 'Standalone installer embedded-payload verification failed.'}
$verificationText=Get-Content -LiteralPath $stdout -Raw
if($verificationText -notmatch 'PASS: embedded payload verified'){throw 'Installer did not produce a verification result.'}
Write-Output $verificationText.Trim()
$result=Get-FileHash -LiteralPath $target -Algorithm SHA256
Set-Content -LiteralPath ($target+'.sha256') -Value ($result.Hash+'  '+[IO.Path]::GetFileName($target)) -Encoding ascii
Write-Output $result
