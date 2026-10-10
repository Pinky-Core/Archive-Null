# Laboratorio y controles — ARCHIVE: NULL

Abrir `Assets/ArchiveNull/Scenes/MechanicsLab.unity` y pulsar Play. No requiere generar una build.

| Control | Acción |
|---|---|
| Mantener G, apuntar y soltar | Rueda: mano, cámara, UV, linterna blanca, pincel con polvo, trípode, marcadores y teléfono recogido |
| Trípode equipado + clic | Previsualizar colocación |
| Q / R o rueda, durante colocación | Rotar el holograma |
| Clic / clic derecho o Esc | Confirmar / cancelar colocación |
| E apuntando al trípode | Montar la herramienta equipada; si está ocupado, recuperarla |
| T apuntando al trípode | Moverlo, conservando el objeto montado |
| L apuntando a una herramienta montada | Encender / apagar |
| P apuntando a una cámara montada | Fotografiar desde su lente |
| F con cámara equipada | Levantar / bajar cámara |
| F con UV o linterna equipada | Encender / apagar |
| Clic con cámara levantada / rueda | Fotografiar / zoom de la lente |
| Clic sostenido con pincel | Aplicar polvo y revelar rastros |
| B | Acceso rápido al mismo pincel de la rueda |
| TAB | Evidencias, libreta y fotografías para descartar |

El trípode comienza vacío. Montar traslada el mismo objeto que estaba en la mano: no genera una segunda cámara o luz. No se puede equipar esa herramienta de nuevo hasta recuperarla del soporte. El teléfono y la mano no se montan. La escala del trípode, las distancias de interacción, el volumen libre y la separación de sus patas se adaptan a la altura del jugador de cada escena. El holograma es cian con apoyo y espacio suficiente, rojo cuando la colocación no es válida. Los modelos siguen siendo prototipos geométricos.

La pantalla de la cámara tiene encuadre, indicadores de enfoque, zoom y obturación. La imagen y las fotos proceden de una cámara hija de la lente física, también en el trípode. Al bajar la cámara, su LCD se apaga a negro. El movimiento corporal incluye pasos a una cadencia limitada, inercia de giro y respiración suave al estar quieto; disminuye al levantar la cámara. Su intensidad se ajusta en Pausa > Configuración > General.

## Memorias y transiciones

Casa: radio fijo de 60 unidades; oficina del contratista: 40; laboratorio: 18. Se configuran con `OfficeDissolveTransition`. El centro no sigue al jugador. La reconstrucción comienza después de retirar ambos fundidos negros y dura como mínimo 4,5 segundos; la retracción dura 2,925 segundos con esa configuración, con cielo negro al inicio y recuperación del skybox original al terminar.

Al entrar desde la oficina, primero se apaga toda la vista: fundido de 0,55 s y al menos 0,4 s completamente negro, además de dos frames, antes de iniciar `LoadSceneAsync`. La barra suaviza su avance hacia el progreso real reportado por Unity. No inventa trabajo pendiente: la activación y el 100 % sólo se confirman al completar sus operaciones.

Al salir se muestra “Cerrando memoria” mientras la onda se retrae hacia el jugador, seguido de “Descargando entorno / recuperando oficina”. En la oficina real sólo se enciende la vista desde negro. `OfficeAssetCache` conserva las dependencias gráficas y de audio de la oficina al salir de ella para evitar releerlas desde disco al regresar; sus objetos de escena se descargan normalmente. Esto consume memoria durante la visita. Al iniciar Play directamente en una memoria, `OfficeWarmup.asset` prepara sus recursos de forma asíncrona mientras se explora; salir antes de completar esa preparación todavía puede requerir esperar. El menú `Archive NULL > Refresh office asset cache` actualiza el listado si se cambian los assets de MainMenu.

La pausa usa una captura desenfocada del mundo, navegación superior y acciones de texto con separadores finos. La captura se calcula una vez al pausar. Los controles aparecen según la herramienta equipada y desaparecen con la mano vacía; el aviso E de interacción se conserva en la retícula. El borde de píxeles mantiene animación en GPU y prueba de profundidad. No se ha certificado un mínimo de 60 FPS en todas las configuraciones del editor.

## Verificación sin builds

`Archive NULL > Test mechanics in editor` ejecuta pruebas en Play Mode. Con Unity cerrado, `Tools/Test-EditorMechanics.ps1` hace lo mismo en una instancia del editor y restaura los PlayerPrefs al finalizar. Los resultados, tiempos medidos y capturas quedan en `Artifacts/EditorValidation`. La prueba recorre laboratorio, casa y oficina, y repite el regreso con las dependencias de la oficina conservadas.

## Marcadores amarillos

En la rueda G, elegir MARCADORES. Clic inicia un holograma: Q/R o la rueda lo rotan, clic confirma y Esc/clic derecho cancela. E sobre un marcador lo recoge. Se numeran consecutivamente por memoria: recoger el 2 de una serie 1–2–3 deja 1–2, con el antiguo 3 renumerado. Las posiciones y el orden se guardan con la partida; las fotografías ya tomadas conservan los números que tenían al capturarse. Se permiten hasta 99 por memoria.

## Escritorio de la oficina

El ordenador usa Archive OS en un Canvas de espacio de mundo sobre la pantalla física del monitor, sin cubrir la cámara del jugador. El cursor es un gráfico propio dentro de esa superficie, con el puntero del sistema oculto durante su uso. Hay sonidos de inicio, apertura, cierre, clic y hover que respetan el volumen de efectos. Las ventanas abren, cierran y minimizan con fundido y movimiento breve; Explorador en la barra de tareas restaura la ventana minimizada. Al apartarse del ordenador, el monitor conserva su sesión y la ventana abierta, pero deja de recibir clics. La transición a una memoria apaga la pantalla.

Archive OS incluye arranque de sesión, aplicaciones y carpetas Expedientes / La llave por dentro / Memorias. La configuración se abre en una ventana del monitor con pestañas General, Sonido, Video, Idioma y Controles, con valores persistentes y reasignación de teclas. MainMenu no crea un menú de pausa ni detiene el tiempo al configurar. El expediente puede releerse desde su carpeta. Se retiraron las dos tarjetas de presentación iniciales. `Archive NULL > Test office monitor in editor` verifica el monitor en el editor ya abierto sin crear una build ni regenerar el laboratorio.

La altura útil de la pantalla se ajusta con `desktopScreenHeight` en el controlador del monitor, conservando el borde superior para que la barra no quede oculta detrás del marco inferior. El Canvas CRT antiguo deja de recibir raycasts y sus textos no son objetivos de puntero. Durante una carga se suspenden los raycasters del entorno saliente para evitar consultar renderers que Unity está descargando. El resultado vigente de las comprobaciones está en `Artifacts/EditorValidation/editor-mechanics.txt`.

Cada inicio de sesión llega de pie al spawn de la puerta, incluso con el tutorial completado. El regreso desde una memoria llega sentado: se identifica por la transición activa, no por una preferencia persistente. Al levantarse se conserva la altura de los ojos de la llegada. El controlador de movimiento propio de la oficina incorpora balanceo corporal, respiración y pasos amortiguados de alfombra en función de la distancia realmente recorrida. Comparte sensibilidad, intensidad corporal, teclas y volumen de efectos con las opciones del monitor. Los sonidos del ordenador y alfombra se sintetizan al iniciar y se liberan al descargar la escena; su timbre se puede evaluar en Play Mode.

La primera sesión exige abrir el expediente físico antes de sentarse o acceder al ordenador. El indicador superior guía: expediente → asiento → escritorio → seleccionar memoria → visor. Los retratos de Julián, Nicolás, Sofía y Víctor están en Resources/CasePortraits/People.png (atlas 2×2, personajes ficticios).

F1-Office requiere registrar la nota del estante de F1-House y leer Mensajes y Llamadas del teléfono; la nota proporciona el PIN. La huella por sí sola no completa esos requisitos. La carpeta de memorias indica cuáles faltan. Un desbloqueo guardado repara un índice antiguo desactualizado al abrir la carpeta.

F1-House oculta trípode y linterna blanca y bloquea sus atajos. La UV, el revelador y los marcadores permanecen disponibles. La colocación usa rayos que excluyen al operador y comprobación de penetración real para evitar falsos obstáculos producidos por los límites de mallas grandes.
