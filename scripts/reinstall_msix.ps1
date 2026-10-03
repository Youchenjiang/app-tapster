# Tapster dev reinstall helper
# Replaces the manual Settings flow (force-stop / uninstall / install) for dev test
# cycles: stop -> remove -> install -> (optional) launch -> report version.
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File scripts/reinstall_msix.ps1
#   powershell -ExecutionPolicy Bypass -File scripts/reinstall_msix.ps1 -Launch
#   powershell -ExecutionPolicy Bypass -File scripts/reinstall_msix.ps1 -RemoveOnly
#   powershell -ExecutionPolicy Bypass -File scripts/reinstall_msix.ps1 -NoStop   # test remove-while-running
param(
    [switch]$Launch,        # launch the app after install
    [switch]$NoStop,        # skip stopping processes first
    [switch]$RemoveOnly,    # only remove, do not install
    [string]$MsixPath = ""  # path to the msix (default: newest in publish/ directory)
)

$ErrorActionPreference = "Stop"
$packageName = "g1014308.Tapster"

# Locate MSIX if not provided
if ([string]::IsNullOrWhiteSpace($MsixPath)) {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    $publishDir = Join-Path $repoRoot "publish"
    if (Test-Path $publishDir) {
        $candidate = Get-ChildItem -Path $publishDir -Filter "Tapster*.msix" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($candidate) {
            $MsixPath = $candidate.FullName
        }
    }
}

if (-not $RemoveOnly) {
    if ([string]::IsNullOrWhiteSpace($MsixPath) -or (-not (Test-Path $MsixPath))) {
        throw "MSIX package not found: '$MsixPath'. Please build it first with scripts/build_msix.ps1"
    }
    $MsixPath = (Resolve-Path $MsixPath).Path
}

# 1. Stop related processes (default; skipped with -NoStop)
if (-not $NoStop) {
    Write-Host "[Stop] Terminating running Tapster instances..." -ForegroundColor Gray
    Get-Process -Name "Tapster", "Tapster.Fluent", "Tapster.Launcher" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# 2. Remove existing package (deployment auto-terminates running processes)
$pkg = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($pkg) {
    Write-Host "[Remove] $($pkg.PackageFullName)" -ForegroundColor Yellow
    Remove-AppxPackage -Package $pkg.PackageFullName
} else {
    Write-Host "[Remove] Not currently installed, skipping." -ForegroundColor Gray
}

if ($RemoveOnly) {
    Write-Host "[Done] Successfully uninstalled $packageName." -ForegroundColor Green
    return
}

# 3. Install
Write-Host "[Install] Sideloading $MsixPath..." -ForegroundColor Cyan
Add-AppxPackage -Path $MsixPath
$pkg = Get-AppxPackage -Name $packageName
if (-not $pkg) { throw "Package verification failed: $packageName not found after installation." }
Write-Host "[OK] Installed v$($pkg.Version) at $($pkg.InstallLocation)" -ForegroundColor Green

# 4. Launch (optional)
if ($Launch) {
    $aumid = $pkg.PackageFamilyName + "!App"
    Write-Host "[Launch] Starting $aumid..." -ForegroundColor Cyan
    Start-Process ("shell:AppsFolder\" + $aumid)
}
