# Telemetría de pruebas de juego

El juego registra eventos locales al jugar en el editor o en el jugador. No requiere servidor. Cada ejecución tiene un identificador aleatorio de sesión; cada caso tiene otro identificador que continúa al reabrir el guardado. Reiniciar la partida comienza otro caso. Una sesión no equivale a una persona.

Los registros nuevos están en `%USERPROFILE%/AppData/LocalLow/PinkyCore/Archive Null/Telemetry/`. Dentro de Unity: **Archive NULL → Telemetría → Abrir registros locales**. `playtests/` contiene partidas normales y `tests/` las regresiones automáticas. No se recopilan nombres de personas, direcciones de red, identificadores del equipo, PIN introducido, notas ni mensajes escritos por el jugador. No se envía información por Internet.

## Obtener el informe

Desde la raíz del proyecto:

```powershell
./Tools/Export-Telemetry.ps1 -Open
```

Genera `Artifacts/TelemetryReport/index.html`, `resumen.json` y doce CSV con separador `;` y UTF-8 con BOM para abrir en Excel. Python 3.10 o posterior es suficiente, sin instalar paquetes. El script busca primero el Python local de las herramientas del proyecto; admite `-PythonExe` para otra instalación.

Para reunir varias pruebas, copiar sus archivos `.jsonl` a una carpeta y ejecutar:

```powershell
./Tools/Export-Telemetry.ps1 -InputDirectory 'D:/PruebasArchive' -OutputDirectory 'D:/InformeArchive' -Open
```

Los duplicados de una misma sesión y secuencia se cuentan una sola vez. Una línea truncada o inválida se omite y aparece como advertencia. No mezclar versiones de diseño al comparar dificultad: los eventos incluyen `version` y el informe trabaja con los archivos suministrados. Copiar únicamente las sesiones de la versión que se desea evaluar. El informe se genera desde una instantánea de los archivos y hay que exportarlo otra vez para actualizarlo.

## Información disponible

| Archivo | Qué permite observar |
|---|---|
| `pistas.csv` | Ranking de recogidas, pistas nunca recogidas, frecuencia por caso y por visita a su escena, mediana y P90 hasta encontrar cada pista, inspecciones y ayudas dirigidas. |
| `casos.csv` | Tiempo activo y observado, resolución, reinicio, número de sesiones, intentos de informe, ayudas y orden de pistas. |
| `sesiones.csv` | Duración, cantidad de eventos y si se observó un cierre normal. |
| `etapas.csv` | Cuántos casos alcanzaron Casa, mensajes, llamadas, Salas, conexiones, reconstrucción e informe; tiempo hasta cada hito. |
| `visitas.csv` / `recorridos.csv` | Tiempo por visita, orden de escenas y revisitas. |
| `intervalos_pistas.csv` | Intervalos entre descubrimientos para localizar tramos con mucha búsqueda o lectura. |
| `intentos.csv` | Intentos de PIN sin su contenido, puntuaciones del informe y campos incorrectos, intentos de cronología y secuencia propuesta de reconstrucción. |
| `acciones.csv` | Fotos tomadas y borradas, inspecciones, conexiones creadas y eliminadas, respaldo narrativo de cada conexión, pistas reveladas, ayudas, chats, pantallas y controles de notas de voz. |
| `herramientas.csv` | Tiempo activo observado con cada herramienta equipada. No demuestra uso efectivo. |
| `posiciones.csv` | Muestras cada 10 s, redondeadas a 0,5 unidades del mapa; el HTML muestra las posiciones activas en una vista superior por escena. |
| `rendimiento.csv` | FPS medios por intervalo, peor frame del intervalo y memoria administrada. No mide toda la RAM ni memoria de GPU. |

Los campos incorrectos del informe son índices 0–4, en el orden de la ficha. `connection_created.value` vale 1 si el par está respaldado por el catálogo narrativo y 0 si no está incluido; un par no catalogado no prueba que el jugador esté razonando mal. Los controles de voz registran posiciones en milisegundos. La búsqueda en una nota de voz no permite afirmar que se escuchó todo el audio.

## Cómo leer las cifras

- **Tiempo activo:** incluye explorar, leer, inspeccionar y escuchar. Excluye pausa, carga, aplicación sin foco y ausencia de entrada durante más de 120 s. Limita saltos de un frame a 1 s para no contar bloqueos prolongados como investigación.
- **Tiempo observado:** tiempo monotónico mientras la aplicación está ejecutándose, incluidas esperas, cargas y falta de foco. No incluye el juego cerrado.
- **Tiempo hasta resolver:** se registra al aceptar por primera vez el informe con el 100 %. Se muestra mediana, P90, mínimo y máximo de casos completos resueltos; no de quienes todavía no terminaron.
- **Partidas reanudadas:** conservan el caso y los tiempos mediante el guardado. Un cierre inesperado puede perder el tiempo posterior al último autoguardado. Los eventos ya escritos siguen disponibles.
- **Progreso previo o comienzo ausente:** se separa del conjunto medido desde cero. Las pistas restauradas se registran como antecedentes, no como nuevas recogidas. Si faltan los registros del inicio del caso, no se incluye en el ranking principal.
- **Denominadores:** la frecuencia por visita usa casos que llegaron a la escena de la pista. La frecuencia por caso usa todos los casos medidos desde cero. Comparar ambas distingue una pista difícil de una escena todavía no alcanzada.
- **Abandono:** se registra un reinicio explícito, pero un caso sin resolver o una sesión sin cierre no demuestra abandono. Puede tratarse de una partida que continuará, un cierre inesperado o una copia incompleta de registros.
- **Muestra pequeña:** los resultados describen la muestra observada. No prueban dificultad general ni preferencias de todos los jugadores. Cuando no hay muestra, se presenta «sin datos» en lugar de inventar porcentajes.

## Verificación

`Tools/Test-Telemetry.py` usa escenarios sintéticos para comprobar tasas, reanudaciones, reinicios, deduplicación, archivos truncados y exportación. `FeedbackEditorProbe` verifica la instrumentación durante el recorrido real por las tres escenas y genera únicamente datos identificados como pruebas. Para revisar esos eventos: `-IncludeTests -OutputDirectory Artifacts/TelemetryReportTests`. Ese informe es técnico y **no contiene estadísticas de jugadores**.

Los registros y los informes locales quedan fuera de Git. El Excel de cambios documenta la implementación; no se usa para presentar partidas de prueba como resultados de usuarios.

Durante la integración se corrigió además la persistencia del inventario: estaba anidado bajo un objeto de la oficina y Unity lo destruía al descargar la escena. El servicio se conserva ahora como objeto raíz; esto mantiene tanto las pistas como los suscriptores que registran su recogida.
