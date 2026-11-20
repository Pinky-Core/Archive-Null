# Feedback de la revisión jugable

El recorrido automatizado en Play Mode completó **62 comprobaciones aprobadas y cero fallos** en Unity 6000.6.0f1. Recorre MainMenu → F1-House → F1-Office → MainMenu, con guardados aislados por ejecución. El informe completo está en [results.txt](../Artifacts/FeedbackValidation/results.txt). No se generó un ejecutable.

## Qué se comprobó

| Área | Resultado |
|---|---|
| Oficina | Altura de llegada y de pie coherentes, sin pausa de memoria y regreso sentado. |
| Archive OS | Botón de cierre de sesión presente; reutiliza la confirmación existente. No se confirma la salida real durante la prueba. |
| Libreta | Raycasts reales alcanzan Reconstruir, Fotos, Eliminar y la confirmación; se elimina la toma seleccionada. |
| Evidencia 2 | La nota conserva información del caso; el PIN se encuentra en una pegatina separada junto al teléfono. |
| Huellas | El rastro de salida no requiere revelado; la huella latente responde al polvo y rechaza UV. |
| Agachado | Ctrl reduce la cápsula activa y la vista; al soltarlo, el personaje se levanta en los pisos reales de Casa y Salas. Un techo sigue impidiendo incorporarse hasta liberar espacio. |
| Inspección | La pegatina se muestra en una capa de primer plano y recupera su capa al devolverla. |
| Teléfono | La rueda equipa el teléfono; las notas tienen icono de reproducción, onda, duración y transcripción opcional. Se comprueban reproducción, pausa, reanudación y avance. |
| Recuerdo eco | Abre una cámara de tercera persona, reproduce voz y libera el estado al cerrar. |
| Salas | Se localiza el monitor, se abren documentos, se registran comprobantes y se recupera el movimiento al salir. |
| Pizarrón | Las tarjetas aumentan de tamaño, la guía avanza a elegir la segunda foto y la vista ampliada presenta su imagen. |
| Telemetría | Mantiene el caso y el inventario entre escenas, recibe recogidas y controles de audio, separa las pruebas y registra una sola resolución. Reiniciar crea otro caso con tiempo cero. El emisor de Sheets permanece desactivado en las regresiones automáticas. |
| Ficha final | Se abre y se captura sin elegir respuestas ni revelar la solución del caso. |

## Hallazgos corregidos durante el recorrido

- Los textos de la libreta interceptaban eventos de puntero. Se excluyen del raycast para dejar accesibles pestañas y acciones.
- El cierre tardío del teléfono detenía la narración de un eco. Cada interfaz detiene ahora únicamente su grupo de audio.
- Algunos monitores importados tienen un nombre genérico en el Renderer. La detección del ordenador revisa también jerarquía, malla y materiales.
- Al regresar a oficina, el pizarrón podía conservar una cámara ausente. Recupera la cámara principal cuando corresponde.
- El HUD y subtítulos normales se superponían al eco. La vista del recuerdo se dibuja por encima.
- Las opciones de pausa mezclaban azul con el esquema amarillo. Se unificaron fondos cálidos y acentos dorados.
- Las tarjetas físicas seguían siendo difíciles de leer a distancia. La vista ampliada al apuntarlas conserva la interacción del tablero sin interceptar clics.
- El inventario de la oficina estaba bajo otro objeto y `DontDestroyOnLoad` no lo conservaba. Ahora se convierte en raíz antes de preservarlo, manteniendo las pistas y los suscriptores de telemetría al cambiar de escena.

El recorrido posterior incluye el texto ampliado del PIN. También reproduce los pasos de MainMenu desde el mismo archivo y con el mismo tono de caminata que F1-House. El bloqueo al levantarse se debía a contactos laterales con el piso importado: la comprobación distingue esos contactos de un techo mediante un barrido ascendente y la dirección de penetración.

## Material para revisión

- [Capturas del editor](../Artifacts/FeedbackValidation/)
- [GIF de reconstrucción](Media/reconstruccion.gif)
- [GIF del recuerdo eco](Media/recuerdo-eco.gif)
- [Video explicativo sin sonido](Media/ArchiveNull_Explicativo_SinAudio.mp4)
- [GDD con capturas y enlaces multimedia](../Assets/ArchiveNull/Documentation/GDD_Archive_NULL-V4.docx)
- [Guía de telemetría local y criterios de interpretación](Telemetry.md)

Para repetir con Unity abierto: `./Tools/Test-Feedback.ps1`. El script solicita el recorrido en la instancia existente y conserva/restaura PlayerPrefs. El comando `Archive NULL > Test feedback changes in editor` también inicia la prueba directamente; para preservar preferencias, utilizar el script. Se usan únicamente escenas y Play Mode, sin iniciar un build.

Las voces del teléfono y los ecos son sintéticas y provisionales: Julián usa una voz argentina y Salas una uruguaya, con ritmos diferentes. La voz del detective conserva sus grabaciones existentes. Su procedencia está en `Assets/ArchiveNull/Resources/CaseAudio/Provenance.txt`. Las siluetas y partículas son una puesta en escena funcional que admite acabado artístico posterior. La automatización no sustituye feedback humano sobre dificultad, interpretación de las pistas, timbre, mareo o rendimiento en otros equipos.

## Cambios reservados por el autor

Esta revisión no modifica el tamaño de la puerta de cocina, el collider de la mesada ni la iluminación de la oficina. Son los tres puntos marcados «YO» en la solicitud.
