param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("major", "minor", "patch", "revision")]
    [string]$Type = "patch",
    
    [Parameter(Mandatory=$false)]
    [switch]$Build
)

$ErrorActionPreference = "Stop"
$root = (Get-Location).Path
$propsPath = "Directory.Build.props"

# 1. Read current version from Directory.Build.props
$content = [System.IO.File]::ReadAllText("$root/$propsPath", [System.Text.Encoding]::UTF8)
if ($content -match '<Version>(?<v>.*)</Version>') {
    $currentVersion = [version]$Matches['v']
} else {
    Write-Error "Could not find Version in $propsPath"
}

# 2. Compute new version
$major = $currentVersion.Major
$minor = $currentVersion.Minor
$patch = $currentVersion.Build
if ($patch -lt 0) { $patch = 0 }
$revision = $currentVersion.Revision
if ($revision -lt 0) { $revision = 0 }

switch ($Type) {
    "major" { $major++; $minor = 0; $patch = 0; $revision = 0 }
    "minor" { $minor++; $patch = 0; $revision = 0 }
    "patch" { $patch++; $revision = 0 }
    "revision" { $revision++ }
}

$newVersion = "$major.$minor.$patch.$revision"
Write-Host "[*] Upgrading version from $currentVersion to $newVersion ..." -ForegroundColor Cyan

$utf8NoBOM = New-Object System.Text.UTF8Encoding($false)

# 3. Update Directory.Build.props
$newProps = $content -replace '<Version>.*</Version>', "<Version>$newVersion</Version>"
[System.IO.File]::WriteAllText("$root/$propsPath", $newProps, $utf8NoBOM)
Write-Host "[Props] Updated $propsPath -> $newVersion" -ForegroundColor Gray

# 4. Update packaging/msix/AppxManifest.xml
$manifestPath = "packaging/msix/AppxManifest.xml"
if (Test-Path "$root/$manifestPath") {
    $manifest = [System.IO.File]::ReadAllText("$root/$manifestPath", [System.Text.Encoding]::UTF8)
    $newManifest = $manifest -replace '(?<=<Identity\s+[^>]*?Version=")([\d\.]+)', $newVersion
    [System.IO.File]::WriteAllText("$root/$manifestPath", $newManifest, $utf8NoBOM)
    Write-Host "[Manifest] Updated $manifestPath -> $newVersion" -ForegroundColor Gray
}

# 5. Insert placeholder into CHANGELOG.md if present
$changelogPath = "CHANGELOG.md"
if (Test-Path "$root/$changelogPath") {
    $changelog = [System.IO.File]::ReadAllText("$root/$changelogPath", [System.Text.Encoding]::UTF8)
    $dateStr = (Get-Date).ToString("yyyy-MM-dd")
    $newEntry = "## [$newVersion] - $dateStr`n`n### Added`n- `n`n### Changed`n- `n`n### Fixed`n- `n`n"
    
    if ($changelog -match '(# CHANGELOG.*?\n\n)') {
        $newChangelog = $changelog -replace '(# CHANGELOG.*?\n\n)', "`$1$newEntry"
    } else {
        $newChangelog = $newEntry + $changelog
    }
    [System.IO.File]::WriteAllText("$root/$changelogPath", $newChangelog, $utf8NoBOM)
    Write-Host "[Changelog] Added $newVersion section to $changelogPath" -ForegroundColor Gray
}

Write-Host "✅ Successfully bumped version to $newVersion" -ForegroundColor Green

if ($Build) {
    Write-Host "[*] Building solution with new version..." -ForegroundColor Cyan
    dotnet build Tapster.sln -c Release
    dotnet build-server shutdown
}
