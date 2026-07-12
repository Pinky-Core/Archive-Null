# Auditoria de assets

Fecha: 12/07/2026

## Alcance

La auditoria toma las escenas habilitadas en `ProjectSettings/EditorBuildSettings.asset`:

- `Assets/ArchiveNull/Scenes/MainMenu.unity`
- `Assets/ArchiveNull/Scenes/F1-House.unity`

Se recorren sus dependencias mediante `AssetDatabase.GetDependencies`. Los archivos de `Resources` y `StreamingAssets` se clasifican aparte porque Unity puede incluirlos aunque ninguna escena tenga una referencia directa.

## Limpieza realizada

- Paquetes `.unitypackage` ya importados: 394,3 MB.
- `Assets/IMPORTADO/Moon Studio`: 389,4 MB sin dependencias de build.
- `Assets/IMPORTADO/Free Wood Door Pack`: 140,8 MB sin dependencias de build.
- Total retirado del proyecto: aproximadamente 924,5 MB.

## Estado posterior

- 1.033 assets auditados.
- 382 candidatos no referenciados.
- 630,9 MB de candidatos restantes en disco.
- 1.418,4 MB ocupados actualmente por `Assets`.

Los candidatos restantes se concentran en packs mixtos que tambien contienen assets utilizados. No deben borrarse como carpetas completas:

- `JustPlay/Computer devices`.
- `Modern Archviz Leafless`.
- `Flashlight`.
- `AK STUDIO ART/Digital Camera`.
- `GeniusCrate_Games/Kitchen_Set`.
- `TextMesh Pro`.

Tambien aparecen escenas deshabilitadas, documentos GDD y contenido futuro. No se eliminaron porque su ausencia de la build actual no significa que el proyecto ya no los necesite.

## Informe detallado

`Logs/BuildAssetUsageAudit.csv` contiene estado, peso, extension y ruta para cada asset. La herramienta puede ejecutarse desde `Archive Null > Tools > Audit Build Asset Usage` en Unity.

## Limitacion

Un asset no referenciado y fuera de `Resources` normalmente no aumenta el peso de la build. Eliminarlo reduce principalmente el tamano del proyecto, tiempos de importacion, reimportacion y copias de seguridad. Para medir el peso final real se debe generar una build y analizar su `BuildReport`.
