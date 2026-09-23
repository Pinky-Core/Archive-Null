param([int]$TimeoutSeconds = 180)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $workspace 'Builds/Windows/Archive NULL.exe'
$outputDirectory = Join-Path $workspace 'Builds/Windows/Validation'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the Windows player first.' }
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

# The player writes save files into a unique validation directory. Preserve its
# Windows PlayerPrefs as well, including tutorial flags changed by scene startup.
$preferencesPath = 'Software\PinkyCore\Archive Null'
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($preferencesPath, $false)
$original = @{}
if ($null -ne $key) {
    foreach ($name in $key.GetValueNames()) {
        $original[$name] = @{ Value = $key.GetValue($name); Kind = $key.GetValueKind($name) }
    }
    $key.Dispose()
}
$original | Export-Clixml -LiteralPath (Join-Path $outputDirectory 'preferences-before-test.xml')
try {
    $arguments = @('-archive-smoke', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11', '-logFile', ('"' + (Join-Path $outputDirectory 'player.log') + '"'))
    $player = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory (Split-Path $executable) -WindowStyle Hidden -PassThru
    if (!$player.WaitForExit($TimeoutSeconds * 1000)) {
        $player.Kill()
        $player.WaitForExit()
        throw 'The smoke test timed out; see Validation/player.log.'
    }
    if ($player.ExitCode -ne 0) { throw "Smoke test failed with exit code $($player.ExitCode). See Validation/smoke.txt." }
    Get-Content -LiteralPath (Join-Path $outputDirectory 'smoke.txt')
}
finally {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($preferencesPath)
    foreach ($name in $key.GetValueNames()) {
        if (!$original.ContainsKey($name)) { $key.DeleteValue($name, $false) }
    }
    foreach ($name in $original.Keys) { $key.SetValue($name, $original[$name].Value, $original[$name].Kind) }
    $key.Dispose()
}
