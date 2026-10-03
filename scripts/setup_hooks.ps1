# Setup local Git hooks for Tapster repository
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sourceHooksDir = Join-Path $repoRoot "scripts\hooks"
$gitHooksDir = Join-Path $repoRoot ".git\hooks"

# 1. Configure git core.hooksPath
try {
    git config core.hooksPath scripts/hooks
    Write-Host "Configured git core.hooksPath -> scripts/hooks" -ForegroundColor Cyan
} catch {
    Write-Warning "Could not set core.hooksPath directly via git config."
}

# 2. Copy hooks into .git/hooks for full backward compatibility
if (Test-Path $gitHooksDir) {
    Get-ChildItem $sourceHooksDir | ForEach-Object {
        $targetFile = Join-Path $gitHooksDir $_.Name
        Copy-Item $_.FullName $targetFile -Force
        Write-Host "Copied fallback git hook: $($_.Name) -> $targetFile"
    }
}

Write-Host "All Git hooks configured successfully!" -ForegroundColor Green
