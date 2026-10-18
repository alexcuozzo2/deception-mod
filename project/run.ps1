# Windows entry point for the authoritative Python build tool.
# Set DECEPTION_PYTHON to an existing Python 3.11+ executable if needed.
$ErrorActionPreference = 'Stop'
$pythonCommand = $env:DECEPTION_PYTHON
if (-not $pythonCommand) {
    $launcher = Get-Command py -ErrorAction SilentlyContinue
    if ($launcher) {
        & $launcher.Source -3 -B "$PSScriptRoot\tools\manage.py" @args
        exit $LASTEXITCODE
    }
    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) { throw 'Python 3.11+ is required. Set DECEPTION_PYTHON to its executable.' }
    $pythonCommand = $python.Source
}
& $pythonCommand -B "$PSScriptRoot\tools\manage.py" @args
exit $LASTEXITCODE
