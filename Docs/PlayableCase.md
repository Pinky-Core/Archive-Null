# ARCHIVE: NULL — Caso jugable

La implementación utiliza el GDD V4 y el documento **La llave por dentro**. El recorrido concentra el caso en las tres escenas habilitadas: oficina de investigación (`MainMenu`), Casa Herrera (`F1-House`) y oficina de Salas (`F1-Office`).

## Cómo jugar

Abrir `Assets/ArchiveNull/Scenes/MainMenu.unity` en Unity o ejecutar `Builds/Windows/Archive NULL.exe`. Para compartir la build, copiar toda la carpeta `Builds/Windows`, conservando el ejecutable, sus DLL y `Archive NULL_Data` juntos.

1. Consultar el expediente y seleccionar Casa Herrera en el terminal. Montar la memoria y usar los cascos VR.
2. Explorar con WASD y mouse. Mantener Ctrl izquierdo permite agacharse; si hay un obstáculo arriba, el personaje permanece agachado hasta tener espacio para levantarse. E inspecciona; mantener clic permite girar el objeto y E lo devuelve con una transición. El objeto sostenido se renderiza por delante del entorno y recupera su capa al devolverlo.
3. Mantener G para elegir herramienta. Con la cámara equipada, F la levanta y clic toma una fotografía; la rueda ajusta el zoom. Las fotografías libres no cuentan como evidencias.
4. Leer la nota del estante: contiene información sobre los documentos. El PIN está en una pegatina junto al teléfono. Recogerlo, equiparlo con la rueda y consultar la conversación con Sofía y el registro de llamadas. Esa combinación habilita la oficina del contratista. Las notas de voz tienen play/pausa, onda para buscar una posición, duración y transcripción desplegable. Julián y Salas usan voces sintéticas rioplatenses diferentes.
5. ESC abre la pausa en las memorias. «Ir a la oficina» conserva los hallazgos. TAB abre evidencias, fotos, libreta y reconstrucción tanto en las memorias como en la oficina.
6. Contrastar las evidencias de ambas memorias en la pizarra. El informe final devuelve una objeción concreta para cada argumento que todavía no esté respaldado.
7. Completar la secuencia de salida en TAB → RECONSTRUIR. Presentar la acusación en el terminal cuando las pruebas y conexiones la sostengan.

La UV permite observar residuos fluorescentes y el pincel con polvo revela huellas latentes de forma permanente. La huella de barro junto a la salida se ve sin herramientas. En F1-Office se puede usar el ordenador de Salas para consultar documentos y registrar sus comprobantes. Inspeccionar la taza o la nota de Julián evoca un recuerdo con siluetas y narración en tercera persona; Escape lo omite. Las voces actuales son sintéticas y provisionales.

En el pizarrón, apuntar a su superficie muestra cómo elegir una foto y luego otra para conectarlas, arrastrarlas o quitar un hilo con clic derecho. El menú Inicio del ordenador principal incluye «Cerrar sesión y salir», con confirmación.

En la oficina, los controles contextuales indican cómo enfocar el monitor y levantarse. Clic derecho permite salir de la vista del monitor y pasar a la posición de pie. Las teclas de interacción y movimiento se pueden reasignar en las opciones existentes.

## Registro y continuidad

### Límite de las memorias

En el objeto de transición de cada memoria, `OfficeDissolveTransition` expone `Limit Memory Radius`, `Maximum Memory Radius`, `Memory Center`, `Memory Origin` (ancla opcional) y `Boundary Width`. El centro se fija al entrar a la escena y nunca sigue al jugador ni a la posición restaurada del guardado. El gizmo cian muestra el perímetro al seleccionar el componente.

El radio explorable se mide horizontalmente en metros de mundo, dejando libertad vertical dentro del mapa. La casa usa 60 m alrededor de (-2.5, 0, 12.5), porque sus pistas ocupan más de 40 m de ancho; la oficina del contratista usa 40 m alrededor de (8, 0, 10.5). El movimiento se detiene antes del borde, teniendo en cuenta el tamaño del jugador. La onda de reconstrucción no expande el área permitida. Los fragmentos del borde parpadean y una malla de píxeles flotantes mantiene visible el perímetro después de la carga. La oficina principal no tiene este límite.

El recorte es visual y el límite de movimiento es jugable; no es un sistema de streaming que descargue assets de la memoria RAM. Si se reduce el radio o se cambia el centro, revisar que las rutas y pistas sigan dentro del perímetro.

- Las evidencias tienen números estables y conservan los IDs anteriores, excepto la taza de la visita, que compartía accidentalmente el ID de la taza de Julián.
- «Fotos / descarte» contiene las tomas libres. Eliminar una toma conserva el registro probatorio y las conexiones. La tarjeta admite 96 tomas; se pueden borrar desde la galería para liberar espacio.
- «Libreta» ofrece notas generales, fichas de Sofía, Nicolás y Víctor, y anotaciones por evidencia. Para anotar una evidencia, seleccionarla antes en la galería y abrir la sección correspondiente de la libreta.
- El guardado incluye evidencias, fotos, notas, pizarra, reconstrucción, cierre del caso y posiciones por memoria. Se guarda también desde la oficina. El JSON se reemplaza de forma atómica y conserva una copia de respaldo.
- Iniciar una investigación nueva elimina también el estado de caso completado. Las opciones de usuario se conservan.
- El movimiento de cabeza se puede reducir a cero en Pausa → General.

## Verificación reproducible

- Unity: `Archive NULL → Validate playable case` revisa las escenas habilitadas, los scripts faltantes, IDs duplicados y las referencias de los argumentos.
- Unity: `Archive NULL → Build playable Windows version` valida y genera la build en `Builds/Windows`.
- PowerShell: `./Tools/Validate-PlayableBuild.ps1` ejecuta las pruebas de integración de la build de desarrollo. Usa una carpeta de guardado propia y restaura los PlayerPrefs del usuario al finalizar. Produce `Builds/Windows/Validation/smoke.txt`, capturas y un log.
- La prueba automática verifica servicios y transiciones reales. No sustituye una sesión humana recorriendo cada habitación y evaluando iluminación, alcance y claridad del misterio.
- También comprueba el disparo fotográfico, la presencia del shader UV en la build y la devolución de objetos inspeccionados a su posición y escala originales. Las capturas `phone-chat.png` y `office-notebook.png` renderizan las interfaces por separado para poder revisar recortes y superposiciones incluso con el proceso de prueba oculto.

La captura usa la API de [solicitudes de render de URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/User-Render-Requests.html) y conserva la proporción de la vista. Las herramientas se ocultan durante la captura y se restauran incluso si ocurre un error.

## Alcance de esta versión

Verificado el 9 de septiembre de 2026 con Unity 6000.6.0f1: build Windows completada con cero errores y ocho advertencias; prueba de integración `PASS ALL`, sin errores de ejecución capturados. Se revisaron las capturas del chat y del cuaderno después de corregir el ancho del contenido y la separación de los títulos. Persisten advertencias de assets sobre colliders con escala negativa y efectos de posprocesado excluidos; no se ha realizado una partida manual completa.

Esta entrega conserva la reconstrucción por orden de acciones y la acusación respaldada. El trípode con holograma y el pincel/revelador se prueban en `MechanicsLab` (ver `Docs/MechanicsLab.md`). Hay dos recuerdos escenificados de presentación provisional; siguen pendientes su acabado artístico y nuevos casos. La reconstrucción de la salida no simula físicamente el hilo. La regresión del feedback y sus límites se documentan en `Docs/Feedback_Testing.md`.
