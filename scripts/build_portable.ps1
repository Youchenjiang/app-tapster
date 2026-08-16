# Tapster Portable Release Build Script (綠色免安裝版)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot/build_common.ps1"

$root = "$PSScriptRoot/.."
$publishDir = "$root/publish/Tapster-Portable"
$zipPath = "$root/publish/Tapster-Portable-v1.1.0.zip"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Tapster Portable Release (v1.1.0)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean previous publish folder
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
New-Item -ItemType Directory -Path $publishDir | Out-Null

# 2. Publish WinUI 3 Fluent GUI (Self-Contained)
Write-Host "[Build] Publishing Tapster.Fluent (Self-Contained WinUI 3)..." -ForegroundColor Yellow
dotnet publish "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir

Assert-NativeSuccess

# 3. Publish NativeAOT CLI into same folder
Write-Host "[Build] Publishing Tapster.Core (NativeAOT CLI)..." -ForegroundColor Yellow
dotnet publish "$root/src/Tapster/Tapster.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o "$root/publish/cli-temp"

if (Test-Path "$root/publish/cli-temp/Tapster.exe") {
    Copy-Item "$root/publish/cli-temp/Tapster.exe" "$publishDir/Tapster.Cli.exe"
}
Remove-Item -Recurse -Force "$root/publish/cli-temp" -ErrorAction SilentlyContinue

# 4. Create ZIP archive
Write-Host "[Zip] Compressing portable release to $zipPath..." -ForegroundColor Green
Compress-Archive -Path "$publishDir/*" -DestinationPath $zipPath -Force

Write-Host "==========================================" -ForegroundColor Green
Write-Host " ✅ Portable Release Complete!" -ForegroundColor Green
Write-Host " Output Folder: $publishDir" -ForegroundColor Gray
Write-Host " ZIP Package:   $zipPath" -ForegroundColor Gray
Write-Host "==========================================" -ForegroundColor Green
