# Shared helpers for Tapster packaging scripts.
$ErrorActionPreference = "Stop"

# Globally disable background compiler worker nodes and servers
$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = "1"

function Stop-BuildServers {
    try {
        & dotnet build-server shutdown *>$null
    } catch { }
}

function Assert-NativeSuccess {
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code $LASTEXITCODE"
    }
}

function Add-WindowsSdkToolsToPath {
    # 1. Check NuGet package tools first (most reliable)
    $nugetRoot = "$env:USERPROFILE/.nuget/packages/microsoft.windows.sdk.buildtools"
    if (Test-Path $nugetRoot) {
        $sdkDirs = Get-ChildItem -Path $nugetRoot -Directory | Sort-Object { $_.Name } -Descending
        foreach ($sdkDir in $sdkDirs) {
            $binX64 = Get-ChildItem -Path "$($sdkDir.FullName)/bin" -Directory | ForEach-Object { "$($_.FullName)/x64" } | Where-Object { Test-Path "$_/makeappx.exe" } | Select-Object -First 1
            if ($binX64) {
                $env:Path = "$binX64;$env:Path"
                Write-Host "[SDK] Found NuGet Windows SDK Build Tools: $binX64" -ForegroundColor Gray
                return
            }
        }
    }

    # 2. Check System Kits
    $kitsRoots = @(
        "C:\Program Files (x86)\Windows Kits\10\bin",
        "C:\Program Files\Windows Kits\10\bin"
    )
    foreach ($rootPath in $kitsRoots) {
        if (Test-Path $rootPath) {
            $sortedDirs = Get-ChildItem -Path $rootPath -Directory |
                          Where-Object { $_.Name -like "10.*" } |
                          Sort-Object { $_.Name } -Descending

            foreach ($dir in $sortedDirs) {
                $candidatePath = Join-Path $dir.FullName "x64"
                if (Test-Path "$candidatePath\makeappx.exe") {
                    $env:Path = "$candidatePath;$env:Path"
                    Write-Host "[SDK] Found System Windows SDK: $candidatePath" -ForegroundColor Gray
                    return
                }
            }
        }
    }

    Write-Warning "[SDK] Could not locate makeappx.exe in standard paths."
}

function Sync-WindowsAppRuntimeDependency {
    param(
        [string]$ProjectPath,
        [string]$ManifestPath
    )

    [xml]$project = Get-Content $ProjectPath
    $sdkReference = $project.Project.ItemGroup.PackageReference |
        Where-Object Include -eq "Microsoft.WindowsAppSDK" |
        Select-Object -First 1
    $sdkVersion = [version]$sdkReference.Version
    if ($sdkVersion.Major -lt 2) {
        throw "Automatic Windows App Runtime alignment requires Windows App SDK 2.0 or newer."
    }

    [xml]$manifest = Get-Content $ManifestPath
    $dependency = $manifest.Package.Dependencies.PackageDependency |
        Where-Object Name -like "Microsoft.WindowsAppRuntime.*" |
        Select-Object -First 1
    if ($dependency) {
        $dependency.Name = "Microsoft.WindowsAppRuntime.$($sdkVersion.Major)"
        $dependency.MinVersion = "$($sdkVersion.ToString(3)).0"
        $manifest.Save($ManifestPath)
        Write-Host "[Build] Windows App Runtime aligned to $($dependency.Name) $($dependency.MinVersion)" -ForegroundColor Gray
    }
}

function Install-DevCertToTrustedPeople {
    param(
        [string]$CertPath,
        [string]$CertPassword = "TapsterDevPassword123!"
    )
    try {
        $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
        Import-PfxCertificate -FilePath $CertPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" -Password $securePassword | Out-Null
        Write-Host "[Cert] Imported certificate into CurrentUser\TrustedPeople for seamless 1-click install." -ForegroundColor Green
    } catch {
        Write-Warning "[Cert] Failed to auto-import to TrustedPeople: $_"
    }
}

function Invoke-SignMsixPackage {
    param(
        [string]$ManifestPath,
        [string]$MsixPath,
        [string]$CertPath,
        [string]$CertPassword = "TapsterDevPassword123!"
    )

    [xml]$manifest = Get-Content $ManifestPath
    $publisher = $manifest.Package.Identity.Publisher
    $certSubject = $publisher

    if (-not (Test-Path $CertPath)) {
        Write-Host "[Cert] Creating dev signing certificate: $CertPath ($certSubject)..." -ForegroundColor Yellow
        $cert = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $certSubject `
            -KeyUsage DigitalSignature `
            -FriendlyName "Tapster Dev Certificate" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

        $securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $CertPath -Password $securePassword | Out-Null
        Write-Host "[Cert] Dev certificate generated." -ForegroundColor Green
    }

    Install-DevCertToTrustedPeople -CertPath $CertPath -CertPassword $CertPassword

    Write-Host "[Sign] Signing MSIX package: $MsixPath..." -ForegroundColor Cyan
    & "signtool.exe" sign /fd SHA256 /a /f $CertPath /p $CertPassword $MsixPath
    Assert-NativeSuccess
    Write-Host "[Sign] MSIX package signed successfully." -ForegroundColor Green
}
