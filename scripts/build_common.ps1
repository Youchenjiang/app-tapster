# Shared helpers for Tapster packaging scripts.
$ErrorActionPreference = "Stop"

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

    Write-Host "[Sign] Signing MSIX package: $MsixPath..." -ForegroundColor Cyan
    & "signtool.exe" sign /fd SHA256 /a /f $CertPath /p $CertPassword $MsixPath
    Assert-NativeSuccess
    Write-Host "[Sign] MSIX package signed successfully." -ForegroundColor Green
}
