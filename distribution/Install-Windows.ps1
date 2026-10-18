param([string]$Game = 'C:\Program Files (x86)\Steam\steamapps\common\The Battle of Polytopia', [switch]$Check)
$ErrorActionPreference = 'Stop'
function Assert-OrdinaryPath([string]$Path) {
    $itemPath = [IO.Path]::GetFullPath($Path)
    while ($itemPath) {
        if (Test-Path -LiteralPath $itemPath) {
            $item = Get-Item -LiteralPath $itemPath -Force
            # OneDrive cloud placeholders are reparse points, but not links.
            if ($item.LinkType) { throw "Linked path refused: $itemPath" }
        }
        $itemPath = [IO.Path]::GetDirectoryName($itemPath)
    }
}
function Digest([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Install-ModAtomically([string]$Source, [string]$Destination, [string]$Expected) {
    Assert-OrdinaryPath $Source
    Assert-OrdinaryPath $Destination
    $existed = [IO.File]::Exists($Destination)
    $previous = if ($existed) { Digest $Destination } else { $null }
    if ($existed -and $previous -eq $Expected) { return }
    $temporary = Join-Path ([IO.Path]::GetDirectoryName($Destination)) ('.deception-update-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $ownedTemporary = $false
    try {
        # Stage beside the mod so the final operation stays on the same volume.
        $output = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $ownedTemporary = $true
        try {
            $inputStream = [IO.File]::OpenRead($Source)
            try { $inputStream.CopyTo($output); $output.Flush($true) }
            finally { $inputStream.Dispose() }
        } finally { $output.Dispose() }
        if ((Digest $temporary) -ne $Expected) { throw 'Staged mod checksum failed.' }
        Assert-OrdinaryPath $Destination
        if ([Diagnostics.Process]::GetProcessesByName('Polytopia').Length) { throw 'Quit Polytopia normally first.' }
        if ($existed) {
            if (![IO.File]::Exists($Destination) -or (Digest $Destination) -ne $previous) { throw 'Mod changed while preparing the update.' }
            # Atomic replacement, deliberately without an old-mod backup.
            [IO.File]::Replace($temporary, $Destination, $null)
        } else {
            # Move refuses a destination created since the initial check.
            [IO.File]::Move($temporary, $Destination)
        }
    } finally {
        if ($ownedTemporary -and [IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}
$Game = [IO.Path]::GetFullPath($Game)
Assert-OrdinaryPath $Game
if (!$Check -and [Diagnostics.Process]::GetProcessesByName('Polytopia').Length) { throw 'Quit Polytopia normally first.' }
$manifest = Get-Content -LiteralPath "$PSScriptRoot\windows-manifest.json" -Raw | ConvertFrom-Json
if ((Digest "$Game\GameAssembly.dll") -ne $manifest.gameSha256) { throw 'Unsupported Steam game build. Expected 2.17.3.16375.' }
$payload = Join-Path $PSScriptRoot 'payload'
$files = @($manifest.files.PSObject.Properties)
$sameConfig = $false
foreach ($required in @('winhttp.dll','BepInEx/plugins/PolyMod.dll','BepInEx/core/dobby.dll','dotnet/coreclr.dll','Mods/alex_deception_windows_candidate.polymod','PolyMod.json')) {
    if ($required -notin $files.Name) { throw "Missing required payload: $required" }
}
foreach ($entry in $files) {
    $rel = $entry.Name
    if ($rel -match '(^/|\\|:|(^|/)\.\.?(/|$))' -or $rel -notmatch '^(BepInEx/core/[^/]+|BepInEx/plugins/PolyMod\.dll|dotnet/[^/]+|Mods/alex_deception_windows_candidate\.polymod|PolyMod\.json|winhttp\.dll|doorstop_config\.ini|\.doorstop_version)$') { throw "Invalid payload path: $rel" }
    $source = Join-Path $payload $rel
    $dest = Join-Path $Game $rel
    Assert-OrdinaryPath $source
    Assert-OrdinaryPath $dest
    if ((Digest $source) -ne $entry.Value) { throw "Package checksum failed: $rel" }
    # Preserve unknown loader installations; ordinary mod upgrades are allowed.
    if ((Test-Path -LiteralPath $dest) -and $rel -notlike 'Mods/*' -and (Digest $dest) -ne $entry.Value) {
        if ($rel -eq 'PolyMod.json') {
            $expected = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
            $actual = Get-Content -LiteralPath $dest -Raw | ConvertFrom-Json
            $sameConfig = @($expected.PSObject.Properties).Count -eq @($actual.PSObject.Properties).Count
            foreach ($property in $expected.PSObject.Properties) {
                if (($property.Value | ConvertTo-Json -Compress) -cne ($actual.($property.Name) | ConvertTo-Json -Compress)) { $sameConfig = $false }
            }
            if ($sameConfig) { continue }
        }
        $knownStockDobby = $rel -eq 'BepInEx/core/dobby.dll' -and (Digest $dest) -eq '8015de7d867245a1095d13947a63763878e4cf5fd3d3089b63cc39200b055ded'
        if (!$knownStockDobby) { throw "Existing loader/config differs: $rel. Preserve it and review before installing this stack." }
    }
}
Assert-OrdinaryPath "$Game\BepInEx\config\deception-windows-candidate.sha256"
$other = @(Get-ChildItem -LiteralPath "$Game\Mods" -Filter '*deception*.polymod' -ErrorAction SilentlyContinue | Where-Object Name -ne 'alex_deception_windows_candidate.polymod')
if ($other.Count) { throw 'Move older Deception archives outside Mods before installing; only one should be enabled.' }
if ($Check) { Write-Output 'Package and destination checks passed. No files changed.'; exit 0 }
foreach ($entry in $files) {
    if ($sameConfig -and $entry.Name -eq 'PolyMod.json') { continue }
    $dest = Join-Path $Game $entry.Name
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest)) | Out-Null
    if ($entry.Name -eq 'Mods/alex_deception_windows_candidate.polymod') {
        Install-ModAtomically (Join-Path $payload $entry.Name) $dest $entry.Value
    } else {
        Copy-Item -LiteralPath (Join-Path $payload $entry.Name) -Destination $dest -Force
    }
    if ((Digest $dest) -ne $entry.Value) { throw "Installed checksum failed: $($entry.Name)" }
}
[IO.Directory]::CreateDirectory("$Game\BepInEx\config") | Out-Null
[IO.File]::WriteAllText("$Game\BepInEx\config\deception-windows-candidate.sha256", $manifest.modSha256+[Environment]::NewLine)
Write-Output 'Deception installed. Open Polytopia through Steam. First launch generates local game bindings.'
