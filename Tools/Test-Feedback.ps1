param([int]$TimeoutSeconds=400)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'RegistrySnapshot.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!(Get-Process Unity -ErrorAction SilentlyContinue | Where-Object {$_.MainWindowTitle -like '*Archive Null*'})){throw 'Abrí el proyecto en Unity antes de ejecutar esta prueba.'}
$request=Join-Path $workspace '.utmp/feedback-test.request'
$result=Join-Path $workspace 'Artifacts/FeedbackValidation/results.txt'
if(Test-Path -LiteralPath $request){throw 'Ya hay una prueba solicitada.'}
$preferences='SoftwarePinkyCoreArchive Null'
$key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($preferences)
$original=@{}
if($null -ne $key){
    foreach($name in $key.GetValueNames()){$original[$name]=@{Kind=$key.GetValueKind($name);Raw=[ArchiveRegistryBytes]::Read($key,$name)}}
    $key.Dispose()
}
try {
    $started=[DateTime]::UtcNow
    New-Item -ItemType Directory -Force -Path (Split-Path $request -Parent)|Out-Null
    [IO.File]::WriteAllText($request,'run')
    while(!(Test-Path -LiteralPath $result) -or (Get-Item -LiteralPath $result).LastWriteTimeUtc -le $started){
        if(([DateTime]::UtcNow-$started).TotalSeconds -gt $TimeoutSeconds){throw 'La prueba no terminó a tiempo. Revisá Unity y el registro del editor.'}
        Start-Sleep -Milliseconds 500
    }
    $lines=Get-Content -LiteralPath $result -Encoding UTF8
    $lines
    if($lines -match '^FAIL '){throw 'La regresión encontró fallos; consultá results.txt.'}
} finally {
    $key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($preferences)
    Restore-ArchiveRegistrySnapshot $key $original
    $key.Dispose()
}
