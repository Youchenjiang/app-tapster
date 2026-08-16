# Tapster MSIX Package Build Script (Windows 現代安裝包 / 微軟商店封裝)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot/build_common.ps1"

$root = "$PSScriptRoot/.."
$packagingDir = "$root/packaging/msix"
$layoutDir = "$packagingDir/Layout"
$publishDir = "$root/publish"
$msixPath = "$publishDir/Tapster-v1.1.0.msix"
$certPath = "$packagingDir/TapsterDev.pfx"

Add-WindowsSdkToolsToPath

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Tapster MSIX Package (v1.1.0)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean previous build & layout
if (Test-Path $layoutDir) { Remove-Item -Recurse -Force $layoutDir }
if (Test-Path $msixPath) { Remove-Item -Force $msixPath }
if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }
New-Item -ItemType Directory -Path $layoutDir | Out-Null

# 2. Publish Tapster.Fluent
Write-Host "[Build] Compiling Tapster.Fluent for MSIX..." -ForegroundColor Yellow
$tempBuild = "$root/publish/msix-temp"
if (Test-Path $tempBuild) { Remove-Item -Recurse -Force $tempBuild }

dotnet publish "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $tempBuild

Assert-NativeSuccess

# 3. Assemble Layout
Write-Host "[Layout] Assembling package files into $layoutDir..." -ForegroundColor Yellow
Copy-Item "$tempBuild/*" "$layoutDir/" -Recurse -Force
Copy-Item "$packagingDir/AppxManifest.xml" "$layoutDir/AppxManifest.xml" -Force
Copy-Item -Recurse "$packagingDir/Assets" "$layoutDir/" -Force

if (Test-Path "$root/Tapster.Fluent/app.ico") {
    Copy-Item "$root/Tapster.Fluent/app.ico" "$layoutDir/app.ico" -Force
}

Remove-Item -Recurse -Force $tempBuild -ErrorAction SilentlyContinue

# 4. Pack MSIX using makeappx.exe
Write-Host "[Pack] Creating MSIX package with makeappx.exe..." -ForegroundColor Cyan
& "makeappx.exe" pack /d "$layoutDir" /p $msixPath /o
Assert-NativeSuccess

# 5. Sign MSIX package using signtool.exe
Invoke-SignMsixPackage `
    -ManifestPath "$packagingDir/AppxManifest.xml" `
    -MsixPath $msixPath `
    -CertPath $certPath

Write-Host "==========================================" -ForegroundColor Green
Write-Host " ✅ MSIX Package Complete!" -ForegroundColor Green
Write-Host " MSIX Package: $msixPath" -ForegroundColor Gray
Write-Host " Certificate:  $certPath" -ForegroundColor Gray
Write-Host "==========================================" -ForegroundColor Green
