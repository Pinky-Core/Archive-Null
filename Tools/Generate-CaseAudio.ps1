param([Parameter(Mandatory=$true)][string]$PiperExe,[Parameter(Mandatory=$true)][string]$VoiceModel)
$ErrorActionPreference='Stop'
$destination=Join-Path $PSScriptRoot '../Assets/ArchiveNull/Resources/CaseAudio'
New-Item $destination -ItemType Directory -Force | Out-Null
$lines=[ordered]@{
    phone_julian='Mañana llevo las copias a Funes. Hay números repetidos y montos distintos. Prefiero confirmarlo antes de denunciar.'
    phone_salas='Estoy llegando. Entro por el lateral, como habíamos quedado. Dejame la taza en la mesa.'
    echo_cup='El té estaba caliente. Escuché pasos en el lateral. Había dejado los papeles sobre la mesa. Después, me costaba mantener los ojos abiertos.'
    echo_phone='Mañana voy a hablar con Funes. No quiero denunciar sin confirmar todo. Voy a dejar el teléfono acá. Alguien acaba de llegar.'
}
$previousEncoding=$OutputEncoding
try {
    $OutputEncoding=[System.Text.UTF8Encoding]::new($false)
    foreach($name in $lines.Keys) {
        $lines[$name] | & $PiperExe --model $VoiceModel --output_file (Join-Path $destination ($name+'.wav'))
        if($LASTEXITCODE -ne 0) {throw "No se pudo sintetizar $name"}
    }
} finally {$OutputEncoding=$previousEncoding}
