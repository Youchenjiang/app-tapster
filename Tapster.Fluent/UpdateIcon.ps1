<#
.SYNOPSIS
    Embeds an .ico file into a Win32 PE executable using UpdateResource API.
    Handles RT_ICON (individual images) and RT_GROUP_ICON (directory) resources.
#>
param(
    [Parameter(Mandatory)][string]$ExePath,
    [Parameter(Mandatory)][string]$IcoPath
)

if (-not (Test-Path $ExePath)) { Write-Error "EXE not found: $ExePath"; exit 1 }
if (-not (Test-Path $IcoPath)) { Write-Error "ICO not found: $IcoPath"; exit 1 }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Win32Res {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern IntPtr BeginUpdateResource(string fileName, bool deleteExisting);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName,
        ushort wLanguage, byte[] lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    public static readonly IntPtr RT_ICON       = (IntPtr)3;
    public static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;
}
'@

# ── Parse ICO file ─────────────────────────────────────────────────────────
$ico = [System.IO.File]::ReadAllBytes($IcoPath)

$idType = [BitConverter]::ToUInt16($ico, 2)
if ($idType -ne 1) { Write-Error "Not a valid ICO file (type=$idType)"; exit 1 }

$count = [BitConverter]::ToUInt16($ico, 4)
Write-Host "ICO file: $count image(s) found"

# ── Build GRPICONDIRENTRY list ──────────────────────────────────────────────
# GRPICONDIR  = WORD reserved(0), WORD type(1), WORD count
# GRPICONDIRENTRY = BYTE width, height, colorCount, reserved
#                   WORD planes, bitCount
#                   DWORD bytesInRes
#                   WORD id  (← replaces imageOffset in raw ICONDIRENTRY)

$grp = [System.Collections.Generic.List[byte]]::new()
# GRPICONDIR header
$grp.AddRange([BitConverter]::GetBytes([uint16]0))       # reserved
$grp.AddRange([BitConverter]::GetBytes([uint16]1))       # type = icon
$grp.AddRange([BitConverter]::GetBytes([uint16]$count))  # count

$icons = @()
for ($i = 0; $i -lt $count; $i++) {
    $base        = 6 + $i * 16
    $width       = $ico[$base]
    $height      = $ico[$base + 1]
    $colorCount  = $ico[$base + 2]
    $reserved    = $ico[$base + 3]
    $planes      = [BitConverter]::ToUInt16($ico, $base + 4)
    $bitCount    = [BitConverter]::ToUInt16($ico, $base + 6)
    $bytesInRes  = [BitConverter]::ToUInt32($ico, $base + 8)
    $imageOffset = [BitConverter]::ToUInt32($ico, $base + 12)

    $imageData = $ico[$imageOffset..($imageOffset + $bytesInRes - 1)]
    $id        = [uint16]($i + 1)

    Write-Host ("  [{0}] {1}x{2} {3}bpp  size={4}" -f $id, $width, $height, $bitCount, $bytesInRes)
    $icons += @{ Id = $id; Data = $imageData }

    # GRPICONDIRENTRY (14 bytes)
    $grp.Add($width);       $grp.Add($height)
    $grp.Add($colorCount);  $grp.Add($reserved)
    $grp.AddRange([BitConverter]::GetBytes($planes))
    $grp.AddRange([BitConverter]::GetBytes($bitCount))
    $grp.AddRange([BitConverter]::GetBytes($bytesInRes))
    $grp.AddRange([BitConverter]::GetBytes($id))   # resource ID (not file offset)
}

$grpBytes = $grp.ToArray()

# ── Write resources into EXE ────────────────────────────────────────────────
$hUpdate = [Win32Res]::BeginUpdateResource($ExePath, $false)
if ($hUpdate -eq [IntPtr]::Zero) {
    $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    Write-Error "BeginUpdateResource failed (Win32 error $err)"
    exit 1
}

foreach ($icon in $icons) {
    $ok = [Win32Res]::UpdateResource(
        $hUpdate, [Win32Res]::RT_ICON, [IntPtr]$icon.Id,
        1033, $icon.Data, [uint32]$icon.Data.Length)
    if (-not $ok) {
        $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
        Write-Warning "UpdateResource RT_ICON id=$($icon.Id) failed (error $err)"
    }
}

# Group icon resource — ID 1, named "MAINICON"
$ok = [Win32Res]::UpdateResource(
    $hUpdate, [Win32Res]::RT_GROUP_ICON, [IntPtr]1,
    1033, $grpBytes, [uint32]$grpBytes.Length)
if (-not $ok) {
    $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    Write-Warning "UpdateResource RT_GROUP_ICON failed (error $err)"
}

$committed = [Win32Res]::EndUpdateResource($hUpdate, $false)
if (-not $committed) {
    $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    Write-Error "EndUpdateResource failed (Win32 error $err)"
    exit 1
}

Write-Host "✅ Icon embedded successfully into: $ExePath"
