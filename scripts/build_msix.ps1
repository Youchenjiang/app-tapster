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

try {
    # 1. Clean previous build & layout
    if (Test-Path $layoutDir) { Remove-Item -Recurse -Force $layoutDir }
    if (Test-Path $msixPath) { Remove-Item -Force $msixPath }
    if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }
    New-Item -ItemType Directory -Path $layoutDir | Out-Null

    # 2. Publish Tapster.Fluent
    Write-Host "[Build] Compiling Tapster.Fluent for MSIX (framework-dependent)..." -ForegroundColor Yellow
    $fluentBuildDir = "$root/Tapster.Fluent/bin/Release"
    $fluentObjDir = "$root/Tapster.Fluent/obj/Release"
    if (Test-Path $fluentBuildDir) { Remove-Item -Recurse -Force $fluentBuildDir }
    if (Test-Path $fluentObjDir) { Remove-Item -Recurse -Force $fluentObjDir }

    dotnet publish "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained false

    Assert-NativeSuccess

    # 3. Assemble Layout & Exclude Unused Heavy AI Dependencies
    Write-Host "[Layout] Assembling package files into $layoutDir..." -ForegroundColor Yellow
    Copy-Item "$packagingDir/AppxManifest.xml" "$layoutDir/AppxManifest.xml" -Force
    Sync-WindowsAppRuntimeDependency `
        -ProjectPath "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
        -ManifestPath "$layoutDir/AppxManifest.xml"

    Copy-Item -Recurse "$packagingDir/Assets" "$layoutDir/" -Force

    $fluentPublishSource = "$root/Tapster.Fluent/bin/Release/net8.0-windows10.0.26100.0/win-x64/publish"
    if (-not (Test-Path $fluentPublishSource)) {
        $fluentPublishSource = "$root/Tapster.Fluent/bin/Release/net8.0-windows10.0.26100.0/win-x64"
    }

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

    Get-ChildItem $fluentPublishSource -File |
        Where-Object {
            $name = $_.Name
            -not ($fluentExclude | Where-Object { $name -like $_ })
        } |
        ForEach-Object {
            Copy-Item $_.FullName "$layoutDir/" -Force
        }

    $runtimeSource = "$fluentPublishSource/runtimes/win-x64/native"
    if (Test-Path $runtimeSource) {
        $runtimeTarget = "$layoutDir/runtimes/win-x64/native"
        New-Item -ItemType Directory -Path $runtimeTarget -Force | Out-Null
        Copy-Item "$runtimeSource/Microsoft.WindowsAppRuntime.Bootstrap.dll" "$runtimeTarget/" -ErrorAction SilentlyContinue
        Copy-Item "$runtimeSource/WebView2Loader.dll" "$runtimeTarget/" -ErrorAction SilentlyContinue
    }

    if (Test-Path "$root/Tapster.Fluent/app.ico") {
        Copy-Item "$root/Tapster.Fluent/app.ico" "$layoutDir/app.ico" -Force
    }

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
}
finally {
    Stop-BuildServers
}
