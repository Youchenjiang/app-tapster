# Tapster Portable Release Build Script (綠色免安裝版)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot/build_common.ps1"

$root = "$PSScriptRoot/.."
$publishDir = "$root/publish"
$tempGuiDir = "$root/publish/gui-temp"
$tempCliDir = "$root/publish/cli-temp"
$targetExe = "$publishDir/Tapster.exe"
$targetCli = "$publishDir/Tapster.Cli.exe"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Tapster Standalone Single-File EXE (v1.1.0)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean previous publish artifacts
if (!(Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }
if (Test-Path $tempGuiDir) { Remove-Item -Recurse -Force $tempGuiDir }
if (Test-Path $tempCliDir) { Remove-Item -Recurse -Force $tempCliDir }
Remove-Item -Recurse -Force "$publishDir/single-test" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$publishDir/Tapster-Portable" -ErrorAction SilentlyContinue
Remove-Item -Force "$publishDir/*.zip" -ErrorAction SilentlyContinue

# 2. Publish WinUI 3 Fluent GUI as True Single-File Executable
Write-Host "[Build] Publishing Tapster as True Standalone Single-File EXE..." -ForegroundColor Yellow
dotnet publish "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $tempGuiDir

Assert-NativeSuccess

# Move Single-File Tapster.exe to publish root
if (Test-Path "$tempGuiDir/Tapster.Fluent.exe") {
    Move-Item -Force "$tempGuiDir/Tapster.Fluent.exe" $targetExe
}
Remove-Item -Recurse -Force $tempGuiDir -ErrorAction SilentlyContinue

$exeSizeMb = [math]::Round(((Get-Item $targetExe).Length / 1MB), 2)

Write-Host "==========================================" -ForegroundColor Green
Write-Host " ✅ Standalone Single-File Release Complete!" -ForegroundColor Green
Write-Host " 🚀 Tapster Single EXE: $targetExe ($exeSizeMb MB)" -ForegroundColor Cyan
Write-Host " 📦 真正純單一 .exe 檔案，免安裝、零依賴、免解壓縮、隨拷隨用！" -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Green
