# Video explicativo

Archivo: [ArchiveNull_Explicativo_SinAudio.mp4](Media/ArchiveNull_Explicativo_SinAudio.mp4).

El montaje explica el objetivo del juego y el recorrido de una investigación: expediente y ordenador, entrada a la memoria, inspección, herramientas, teléfono, recuerdos eco, oficina de Salas, pizarrón, reconstrucción e informe final. Usa capturas y secuencias del prototipo, con texto lateral para mantener visible el juego, movimientos suaves sobre imágenes fijas y fundidos entre capítulos. No revela la identidad del responsable ni la solución completa.

El guion está en `Tools/ExplainerStoryboard.json`. La duración prevista es **160 segundos (2:40)**, con salida **1920 × 1080, 30 fps y sin pista de audio**. Las secuencias de la regresión tienen una cadencia de captura menor; el archivo a 30 fps no implica que sean grabaciones de gameplay continuo a esa frecuencia.

Para regenerar desde las capturas del editor:

```powershell
./Tools/Export-ExplainerVideo.ps1 -FfmpegExe 'RUTA/ffmpeg.exe'
```

`-PreviewChapter 5 -OutputFile .utmp/preview.mp4` permite revisar un capítulo antes de exportar todo. `Tools/Export-GameplayMedia.ps1` también invoca este montaje al regenerar los GIF y el video. Las fuentes del recorrido están en `Artifacts/FeedbackValidation/` y `.utmp/FeedbackFrames/`; las secuencias temporales deben volver a capturarse con la regresión si no existen en el equipo.
