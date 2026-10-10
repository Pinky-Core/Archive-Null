param([int]$TimeoutSeconds = 240, [switch]$OfficeOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'RegistrySnapshot.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*Archive Null*' }) {
    throw 'Use Archive NULL > Test mechanics in editor in the existing editor session.'
}
$outputDirectory = Join-Path $workspace 'Artifacts/EditorValidation'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$preferencesPath = 'Software\PinkyCore\Archive Null'
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($preferencesPath, $false)
$original = @{}
if ($null -ne $key) {
    foreach ($name in $key.GetValueNames()) { $original[$name] = @{ Raw = [ArchiveRegistryBytes]::Read($key,$name); Kind = $key.GetValueKind($name) } }
    $key.Dispose()
}
$original | Export-Clixml -LiteralPath (Join-Path $outputDirectory 'preferences-before-test.xml')
try {
    $arguments = '-batchmode -projectPath "' + $workspace + '" -executeMethod MechanicsLabEditor.TestInEditor -archive-editor-test -logFile "' + (Join-Path $outputDirectory 'editor.log') + '"'
    if ($OfficeOnly) { $arguments += " -archive-office-test" }
    $editor = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$editor.WaitForExit($TimeoutSeconds * 1000)) { $editor.Kill(); $editor.WaitForExit(); throw 'Editor test timed out; inspect Artifacts/EditorValidation/editor.log.' }
    if ($editor.ExitCode -ne 0) { throw 'Editor mechanics test failed; inspect Artifacts/EditorValidation.' }
    Get-Content -LiteralPath (Join-Path $outputDirectory 'editor-mechanics.txt')
}
finally {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($preferencesPath)
    Restore-ArchiveRegistrySnapshot $key $original
    $key.Dispose()
}
