# Setup local Git hooks for Tapster repository
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$gitHooksDir = Join-Path $repoRoot ".git\hooks"
$sourceHooksDir = Join-Path $repoRoot "scripts\hooks"

if (-not (Test-Path $gitHooksDir)) {
    Write-Error ".git/hooks directory not found. Please ensure this is run inside a git repository."
}

Get-ChildItem $sourceHooksDir | ForEach-Object {
    $targetFile = Join-Path $gitHooksDir $_.Name
    Copy-Item $_.FullName $targetFile -Force
    Write-Host "Installed git hook: $($_.Name) -> $targetFile"
}

Write-Host "All Git hooks installed successfully!" -ForegroundColor Green
