# ARCHIVE: NULL — Plan de mejoras

Estado: propuesta basada en la revisión del código del 8 de septiembre de 2026. No implica que las funcionalidades estén implementadas ni verificadas en Play Mode.

## 1. Interacción y presentación

### Inspección de objetos
- Animar el recorrido desde la posición real del objeto hasta las manos y su devolución, interpolando posición y rotación.
- Conservar el mismo objeto visible durante todo el movimiento. Restaurar jerarquía, colisiones y física al terminar o interrumpir la interacción.
- Base: `InspectObject.cs`, que actualmente asigna directamente la posición y rotación de inspección.
- Aceptación: tomar, girar, devolver y pausar durante la transición sin saltos visibles ni objetos perdidos.

### Celular
- Ajustar marco, proporción de pantalla, márgenes, navegación y jerarquía de mensajes para que se reconozca como un teléfono.
- Permitir desplazamiento de conversaciones largas y mantener texto y controles dentro de la pantalla.
- Base: `PhoneEvidenceReader.cs`.
- Aceptación: PIN, chats y llamadas legibles y navegables en 16:9, 16:10 y ultrawide, incluyendo mensajes largos.

### Cámara física y disparo
- Colocar la cámara algo más lejos del rostro: el jugador debe ver el cuerpo y su pantalla, con parte del entorno alrededor.
- Mostrar el encuadre en la pantalla del dispositivo y alinear la foto con ese encuadre.
- Añadir cierre breve de obturador y coordinarlo con sonido y flash. Ya existe feedback de flash; revisar su presentación y sincronización.
- Base: `EvidenceCameraController.cs` y los modelos de cámara disponibles en Assets.
- Aceptación: levantar y bajar la cámara con continuidad; la imagen guardada coincide con el encuadre y no contiene controles ni el efecto del disparo.

### Pausa y movimiento de cabeza
- Revisar jerarquía visual, selección, espaciado y navegación del menú de pausa.
- Agregar oscilación de cabeza sutil vinculada al desplazamiento real y al contacto con el suelo, con transición suave al detenerse.
- Incorporar ajuste de intensidad, incluido cero, y evitar oscilación durante inspección, pausa o lectura.
- Base: `GlobalPauseMenu.cs`, `FirstPersonMovement.cs`, `FirstPersonLook.cs` y configuración de asistencia.
- Aceptación: caminar contra una pared no produce balanceo de marcha; detenerse recupera una pose estable; abrir y cerrar pausa conserva el estado correcto del cursor y los controles.

## 2. Fotografías y documentación del caso

### Fotografía libre y descarte
- Permitir fotografiar cualquier encuadre, aunque no contenga evidencia reconocida.
- Guardar las fotos en una colección independiente de las evidencias validadas, con vínculo opcional a la evidencia correspondiente.
- Mantenerlas al salir de la memoria y ofrecer revisión y eliminación individual en la oficina.
- Persistir imágenes y metadatos; liberar las texturas y archivos que ya no se necesiten al eliminar una foto.
- Base: `EvidenceCameraController.cs`, `EvidenceNotebookUI.cs`, `EvidenceInventory.cs`, `GameSaveSystem.cs` y tablero de oficina.
- Dependencia crítica: el desbloqueo actual en `EvidenceDrivenMemoryUnlock.cs` cuenta seis evidencias. Las fotos libres no deben incrementar ese contador. Eliminar una imagen no debe borrar automáticamente una deducción ni invalidar conexiones del caso.
- Aceptación: fotografiar un objeto sin evidencia, regresar a la oficina, reiniciar la partida y encontrar la imagen; eliminarla y comprobar que no reaparece ni altera el progreso.

### Tags numéricos y formulario
- Asignar números estables por caso a las evidencias; mantener la identidad interna existente para guardado y conexiones.
- Mostrar el mismo número en etiquetas, fotografías vinculadas, libreta y tablero. No renumerar por eliminar una fotografía.
- Crear formulario editable con dos secciones: sospechosos y evidencias.
- Campos propuestos para sospechosos: nombre, relación con la víctima, coartada, observaciones y evidencias relacionadas.
- Campos propuestos para evidencias: número, ubicación, descripción y observaciones del jugador.
- Guardar lo escrito y mantener las hipótesis del jugador diferenciadas de los hechos descubiertos.
- Aceptación: registrar pistas en distinto orden, completar fichas, guardar y cargar sin perder campos ni referencias.

## 3. Herramientas y manipulación física

- Unificar equipar, guardar, soltar, colocar y recuperar herramientas y objetos transportables.
- Validar superficies antes de colocar; recuperar física y colisiones al soltar y evitar penetración del suelo.
- Incorporar trípode desplegable con soporte para luz o cámara, orientación ajustable y recuperación del equipo montado.
- Agregar pincel con polvo para huellas y revelador para rastros de sangre como interacciones diferenciadas.
- Aprovechar `UvRevealTarget.cs` como base de exposición/revelado, definiendo para cada rastro la herramienta y condiciones correctas.
- Revisar `LiftableObject.cs`, que ya permite desparentar un objeto transportado, antes de extender ese comportamiento a todo el inventario.
- Aceptación: colocar y recuperar trípode y accesorio, soltar en pendientes y esquinas, revelar un rastro y fotografiar el resultado sin duplicar objetos ni perder equipo.

## 4. Reconstrucción de memoria

### Onda expansiva de carga
- Usar un origen compartido en coordenadas de mundo y un radio animado para toda la reconstrucción.
- Cada superficie aparece cuando la alcanza el frente de onda; el ruido queda limitado al borde para aportar textura.
- Coordinar shader y controlador con los mismos parámetros de origen, radio y ancho del frente.
- Base: `ArchiveRoomDissolve.shader`, `OfficeDissolveTransition.cs` y `SceneRebuildTransition.cs`.
- El shader actual usa ruido en coordenadas de mundo y una cantidad de disolución; falta el criterio de distancia radial.
- Aceptación: objetos a igual distancia aparecen a la vez aunque usen materiales distintos; un objeto grande se revela progresivamente al cruzarlo la onda.

### Flashbacks y objetos eco
- Definir objetos eco y requisitos de puzzle/evidencias que habilitan fragmentos concretos del recuerdo.
- Registrar qué fragmentos se reconstruyeron y permitir volver a la investigación conservando su estado.
- Establecer cómo se revisitan los recuerdos y cómo se interrumpe una secuencia sin bloquear controles.
- Aceptación: resolver el requisito activa el fragmento previsto, guardar/cargar conserva su estado y no permite saltar requisitos mediante fotos libres.

## 5. Nuevos crímenes

Diseñar los casos después de probar las mecánicas compartidas. Cada caso debe introducir una interacción y combinarla después con las ya aprendidas.

Propuestas de estructura, pendientes de guion:
- Caso de introducción: tags, fotografía libre y formulario de sospechosos/evidencias.
- Caso de rastros ocultos: pincel, revelador y documentación del hallazgo.
- Caso de reconstrucción espacial: trípode, iluminación y comparación de encuadres.
- Caso de recuerdos fragmentados: objetos eco y puzzles que ordenan o completan una secuencia.

Cada caso necesita un mapa de pistas, requisitos de avance, hipótesis posibles y resolución comprobable.

## Orden sugerido y validación

1. Inspección, celular, cámara física, pausa y movimiento: resolver la experiencia inmediata.
2. Colección de fotos independiente, persistencia y descarte; después tags y formulario.
3. Manipulación común de herramientas; después trípode y reveladores.
4. Onda radial y sistema de fragmentos de memoria activados por puzzles.
5. Crear nuevos casos sobre las mecánicas verificadas.

Al cerrar cada etapa: compilar en Unity, probar el flujo en las escenas reales y revisar guardado/carga cuando cambien datos persistentes. Las interfaces requieren comprobación visual; la lectura del código no confirma su aspecto ni reproduce por sí sola los recortes reportados.
