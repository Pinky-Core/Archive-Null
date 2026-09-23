# ARCHIVE: NULL — Caso jugable

La implementación utiliza el GDD V4 y el documento **La llave por dentro**. El recorrido concentra el caso en las tres escenas habilitadas: oficina de investigación (`MainMenu`), Casa Herrera (`F1-House`) y oficina de Salas (`F1-Office`).

## Cómo jugar

Abrir `Assets/ArchiveNull/Scenes/MainMenu.unity` en Unity o ejecutar `Builds/Windows/Archive NULL.exe`. Para compartir la build, copiar toda la carpeta `Builds/Windows`, conservando el ejecutable, sus DLL y `Archive NULL_Data` juntos.

1. Consultar el expediente y seleccionar Casa Herrera en el terminal. Montar la memoria y usar los cascos VR.
2. Explorar con WASD y mouse. E inspecciona; mantener clic permite girar el objeto y E lo devuelve con una transición.
3. Mantener G para elegir herramienta. Con la cámara equipada, F la levanta y clic toma una fotografía; la rueda ajusta el zoom. Las fotografías libres no cuentan como evidencias.
4. Leer la nota del estante: contiene información sobre los documentos y el PIN del teléfono. Recoger el teléfono y consultar la conversación con Sofía y el registro de llamadas. Esa combinación habilita la oficina del contratista.
5. ESC abre la pausa en las memorias. «Ir a la oficina» conserva los hallazgos. TAB abre evidencias, fotos, libreta y reconstrucción tanto en las memorias como en la oficina.
6. Contrastar las evidencias de ambas memorias en la pizarra. El informe final devuelve una objeción concreta para cada argumento que todavía no esté respaldado.
7. Completar la secuencia de salida en TAB → RECONSTRUIR. Presentar la acusación en el terminal cuando las pruebas y conexiones la sostengan.

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

Esta entrega cierra el caso existente con una reconstrucción por orden de acciones y una acusación respaldada. Los prototipos de trípode con holograma y pincel/revelador se prueban ahora en `MechanicsLab` (ver `Docs/MechanicsLab.md`). Siguen pendientes el acabado artístico, soltar y recuperar todas las herramientas, flashbacks escenificados y nuevos casos. La reconstrucción actual no simula físicamente el hilo ni reemplaza esas ampliaciones.
