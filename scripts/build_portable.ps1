# Tapster Standalone Single-File EXE Build Script (綠色純單檔免安裝版)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot/build_common.ps1"

$root = "$PSScriptRoot/.."
$publishDir = "$root/publish"
$tempFluent = "$publishDir/fluent-temp"
$launcherDir = "$root/src/Tapster.Launcher"
$payloadZip = "$launcherDir/payload.zip"
$targetExe = "$publishDir/Tapster.exe"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Tapster True Single-File EXE (v1.1.0)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

try {
    # 1. Clean previous build artifacts
    if (!(Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }
    if (Test-Path $tempFluent) { Remove-Item -Recurse -Force $tempFluent }
    if (Test-Path $payloadZip) { Remove-Item -Force $payloadZip }
    if (Test-Path $targetExe) { Remove-Item -Force $targetExe }
    Remove-Item -Recurse -Force "$publishDir/trim-test" -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "$publishDir/single-test" -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "$publishDir/Tapster-Portable" -ErrorAction SilentlyContinue

    # 2. Publish Tapster.Fluent (Self-Contained & Trimmed)
    Write-Host "[Build] Compiling Tapster.Fluent core engine..." -ForegroundColor Yellow
    dotnet publish "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishTrimmed=true `
        -p:TrimMode=partial `
        -o $tempFluent

    Assert-NativeSuccess

    # 3. Exclude Unused Heavy AI Dependencies from Payload
    Write-Host "[Prune] Stripping unused AI and debug payloads..." -ForegroundColor Yellow
    $fluentExclude = @(
        "*.pdb",
        "DirectML.dll",
        "onnxruntime.dll",
        "Microsoft.Windows.AI.MachineLearning.dll",
        "Microsoft.Windows.AI.*",
        "NPUDetect.dll",
        "PerceptiveStreaming.dll",
        "Microsoft.Windows.ApplicationModel.Background.UniversalBGTask.dll",
        "workloads.*.json"
    )

    Get-ChildItem $tempFluent -File |
        Where-Object {
            $name = $_.Name
            ($fluentExclude | Where-Object { $name -like $_ })
        } |
        ForEach-Object {
            Remove-Item $_.FullName -Force
        }

    # 4. Compress trimmed payload into payload.zip for Launcher embedding
    Write-Host "[Zip] Compressing payload into Launcher resource..." -ForegroundColor Yellow
    Compress-Archive -Path "$tempFluent/*" -DestinationPath $payloadZip -Force
    Remove-Item -Recurse -Force $tempFluent -ErrorAction SilentlyContinue

    # 5. Compile Tapster.Launcher into Single-File Standalone EXE
    Write-Host "[Launcher] Compiling single-file Tapster.exe launcher..." -ForegroundColor Cyan
    dotnet publish "$launcherDir/Tapster.Launcher.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true `
        -p:TrimMode=partial `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o "$publishDir/launcher-temp"

    Assert-NativeSuccess

    if (Test-Path "$publishDir/launcher-temp/Tapster.exe") {
        Move-Item -Force "$publishDir/launcher-temp/Tapster.exe" $targetExe
    }
    Remove-Item -Recurse -Force "$publishDir/launcher-temp" -ErrorAction SilentlyContinue
    Remove-Item -Force $payloadZip -ErrorAction SilentlyContinue

    $exeSizeMb = [math]::Round(((Get-Item $targetExe).Length / 1MB), 2)

    Write-Host "==========================================" -ForegroundColor Green
    Write-Host " ✅ Standalone Single-File Release Complete!" -ForegroundColor Green
    Write-Host " 🚀 Output EXE: $targetExe ($exeSizeMb MB)" -ForegroundColor Cyan
    Write-Host " 📦 真正純單一 .exe 檔案，點擊直接開跑、零報錯、超穩定！" -ForegroundColor Yellow
    Write-Host "==========================================" -ForegroundColor Green
}
finally {
    Stop-BuildServers
}
