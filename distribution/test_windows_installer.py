"""Installer contract checks; optional PowerShell tests use only disposable files."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SOURCE = Path(__file__).with_name('Install-Windows.ps1').read_text()
FUNCTIONS = SOURCE[SOURCE.index('function Assert-OrdinaryPath'):SOURCE.index('$Game = [IO.Path]')]
POWERSHELL = shutil.which('pwsh') or shutil.which('powershell')


class InstallerContractTests(unittest.TestCase):
    def test_atomic_mod_route_and_no_backup(self):
        self.assertIn('[IO.File]::Replace($temporary, $Destination, $null)', FUNCTIONS)
        self.assertIn('[IO.File]::Move($temporary, $Destination)', FUNCTIONS)
        self.assertIn('[IO.FileMode]::CreateNew', FUNCTIONS)
        self.assertIn('$output.Flush($true)', FUNCTIONS)
        self.assertIn("if ($entry.Name -eq 'Mods/alex_deception_windows_candidate.polymod') {\n"
                      '        Install-ModAtomically (Join-Path $payload $entry.Name) $dest $entry.Value', SOURCE)
        self.assertLess(FUNCTIONS.index('Staged mod checksum failed.'), FUNCTIONS.index('[IO.File]::Replace'))
        self.assertLess(FUNCTIONS.index('Mod changed while preparing the update.'), FUNCTIONS.index('[IO.File]::Replace'))
        self.assertIn('if ($ownedTemporary -and [IO.File]::Exists($temporary))', FUNCTIONS)

    @unittest.skipUnless(POWERSHELL, 'PowerShell unavailable; native installer execution unverified')
    def test_disposable_replace_create_and_failed_digest(self):
        # Only function definitions are loaded, never the installer entry point.
        script = "$ErrorActionPreference = 'Stop'\n" + FUNCTIONS + r'''
$source = Join-Path $PSScriptRoot 'candidate'
$dest = Join-Path $PSScriptRoot 'installed.polymod'
[IO.File]::WriteAllText($source, 'new candidate')
[IO.File]::WriteAllText($dest, 'old candidate')
$expected = Digest $source
$before = Digest $dest
$rejected = $false
try { Install-ModAtomically $source $dest ('0' * 64) } catch { $rejected = $true }
if (!$rejected -or (Digest $dest) -ne $before) { throw 'Bad digest damaged existing mod.' }
Install-ModAtomically $source $dest $expected
if ((Digest $dest) -ne $expected) { throw 'Replacement failed.' }
Install-ModAtomically $source $dest $expected
[IO.File]::Delete($dest)
Install-ModAtomically $source $dest $expected
if ((Digest $dest) -ne $expected) { throw 'Fresh creation failed.' }
if (@(Get-ChildItem -LiteralPath $PSScriptRoot -Force -Filter '.deception-update-*').Count) { throw 'Staging leak.' }
if (@(Get-ChildItem -LiteralPath $PSScriptRoot -File).Count -ne 3) { throw 'Unexpected backup or extra file.' }
'''
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'test.ps1'
            path.write_text(script)
            result = subprocess.run([POWERSHELL, '-NoProfile', '-File', str(path)],
                                    capture_output=True, text=True, timeout=30)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main(verbosity=2)
