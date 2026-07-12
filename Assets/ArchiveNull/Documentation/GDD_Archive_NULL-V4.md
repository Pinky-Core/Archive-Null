# GAME DESIGN DOCUMENT

## Archive: NULL

**Alumno:** Caporaloni Luca  
**Materia:** Proyecto de Integración: Desarrollo e Implementación de Videojuegos  
**Versión:** 4.0  
**Fecha:** 10/07/2026  
**Estado:** Prototipo universitario en desarrollo

## 1. Resumen ejecutivo

Archive: NULL es un juego de investigación narrativa y puzzles en primera persona para PC. El jugador interpreta al Operador 253, integrante de una división de reconstrucción forense que utiliza memorias digitales para revisar escenas relacionadas con un crimen.

El caso principal, “La llave por dentro”, comienza con la muerte de Julián Herrera en una casa cerrada desde adentro. La escena parece indicar suicidio, pero contiene evidencia real, circunstancial y plantada. El jugador debe registrar objetos, consultar dispositivos, revelar rastros con luz UV, comparar documentos y conectar evidencia en la oficina para formular una acusación argumentada.

La experiencia no premia recolectar todos los objetos sin analizarlos. El progreso depende de comprender relaciones entre método, móvil, acceso, horario y manipulación de escena.

## 2. Deseo del proyecto

El juego debe expresar la experiencia de reconstruir un crimen a partir de información incompleta y potencialmente engañosa. La intención es que el jugador dude de la primera explicación, observe el contexto y construya una hipótesis defendible.

La recompensa principal es intelectual: detectar una contradicción, reinterpretar una pista y demostrar por qué una explicación es más coherente que otra.

El proyecto universitario debe demostrar integración entre narrativa, diseño de niveles, programación, interfaz, persistencia, sonido y experiencia de usuario.

## 3. Fantasía del jugador

El jugador es un operador forense especializado en reconstrucciones de memoria. No presencia el crimen ni controla a la víctima. Trabaja desde una oficina, recibe un expediente, ingresa en memorias reconstruidas, documenta evidencia y vuelve al espacio de análisis para conectar información.

La fantasía se sostiene mediante cuatro señales:

- Un expediente asignado con víctima, implicados y contexto preliminar.
- Herramientas forenses con funciones diferenciadas.
- Memorias presentadas como reconstrucciones parciales, no como verdad objetiva.
- Una acusación final que exige justificar método, móvil, acceso y descarte de sospechosos.

## 4. Valores nucleares

### 4.1 La evidencia necesita contexto

Una huella demuestra contacto, no culpabilidad. Un conflicto demuestra tensión, no asesinato. Cada pista debe adquirir significado al relacionarse con lugar, horario y otras evidencias.

### 4.2 La primera explicación puede estar construida

La escena presenta una lectura inicial convincente, pero el jugador debe detectar elementos colocados para dirigir la investigación.

### 4.3 Investigar es interpretar

Fotografiar y recoger objetos son acciones de registro. El progreso real ocurre al comparar, conectar y formular hipótesis.

### 4.4 Toda acción debe responder

Inspeccionar, fotografiar, revelar, recoger y conectar deben producir feedback visual, sonoro o narrativo inmediato.

## 5. Pilares de juego

### 5.1 Observación forense

Recorrer espacios, leer posiciones, inspeccionar objetos y detectar rastros que no son visibles a simple vista.

### 5.2 Registro de evidencia

Usar cámara, teléfono, interacción directa y luz UV para incorporar información al expediente persistente.

### 5.3 Interpretación y contradicción

Comparar mensajes, horarios, documentos, rastros y testimonios para distinguir evidencia real, circunstancial y plantada.

### 5.4 Reconstrucción argumentada

Conectar evidencia en la pizarra y responder preguntas sobre método, móvil, acceso, manipulación y culpable.

## 6. Abstracciones clave

### Expediente

Es la fuente inicial y progresiva de información oficial. Contiene datos de víctima, implicados, estado del caso y conclusiones desbloqueadas. No es un inventario: organiza el caso.

### Evidencia

Es una unidad verificable obtenida dentro de una memoria o dispositivo. Incluye nombre, descripción, procedencia, imagen y comentario narrativo. Se guarda en la galería y puede utilizarse en conexiones.

### Libreta

Es un espacio de escritura libre del jugador. No modifica descripciones oficiales ni valida conclusiones.

### Memoria

Es una reconstrucción explorable de un lugar o momento. Puede contener información incompleta y debe reinterpretarse al obtener nuevas pistas.

### Pizarra

Es la herramienta visual para ordenar fotografías, notas y conexiones. Una conexión no es automáticamente correcta: el sistema responde si la relación tiene sustento o agrega ruido.

### Conclusión

Es una afirmación desbloqueada al reunir y relacionar evidencia suficiente. Ejemplo: “Julián fue intoxicado mediante una bebida”.

## 7. Público, plataforma y alcance

**Género:** Investigación narrativa / puzzle en primera persona.  
**Plataforma:** PC.  
**Modo:** Un jugador, offline.  
**Control:** Teclado y mouse.  
**Duración objetivo comercial:** 1 a 2 horas.  
**Duración objetivo del prototipo académico:** 20 a 35 minutos.  
**Clasificación orientativa:** Adolescentes y adultos; crimen sin violencia gráfica explícita como foco principal.

## 8. Bucle principal

1. Recibir y leer el expediente en la oficina.
2. Identificar el objetivo de investigación actual.
3. Montar una memoria desde el monitor.
4. Equipar el visor e ingresar en la reconstrucción.
5. Explorar, inspeccionar y registrar evidencia.
6. Resolver el puzzle local o reunir evidencia suficiente.
7. Volver a la oficina.
8. Revisar galería, expediente y pizarra.
9. Conectar evidencia y desbloquear una conclusión o memoria.
10. Formular una acusación cuando estén cubiertos método, móvil, acceso y manipulación.

## 9. Controles

**WASD:** Movimiento.  
**Mouse:** Vista y selección.  
**E:** Interactuar, inspeccionar o dejar un objeto inspeccionado.  
**G:** Abrir rueda de herramientas.  
**F con cámara equipada:** Llevar cámara a la cara o bajarla.  
**F con UV equipada:** Encender o apagar luz UV.  
**Clic izquierdo con cámara abierta:** Fotografiar.  
**Clic izquierdo durante inspección:** Rotar objeto.  
**Rueda durante inspección o cámara:** Acercar o alejar.  
**Tab:** Abrir libreta y galería.  
**Esc:** Pausa.  
**Clic derecho en Far:** Levantarse del asiento.  
**Esc estando parado en la oficina:** Acceder al monitor.

La interfaz contextual muestra únicamente los controles relevantes al estado actual.

## 10. Herramientas

### Mano

Herramienta predeterminada. Permite interactuar, recoger, abrir dispositivos e inspeccionar objetos.

### Cámara

Se equipa desde la rueda. Con F se lleva a la cara. Permite fotografiar EvidenceTarget dentro de distancia y encuadre. El zoom amplía lectura visual, pero no es obligatorio para registrar evidencia cercana.

### Luz UV

Se enciende con F. Revela manchas, polvo y huellas ocultas solamente dentro del área apuntada. Los rastros se dividen en zonas independientes para evitar revelar el objeto completo de una vez.

### Objetos recogidos

Cuarta categoría de la rueda. Contiene dispositivos y objetos relevantes, como el teléfono de Julián. Al abrir un dispositivo se bloquean movimiento e interacciones exteriores.

## 11. Mecánicas y reglas

### 11.1 Inspección

- Requiere mirar un objeto marcado y pulsar E.
- El objeto se centra en el punto de inspección.
- El jugador rota el punto de inspección, no el pivote importado del modelo.
- E finaliza la inspección y restaura posición, rotación y jerarquía.
- Esc abre pausa sin abandonar la inspección.
- La inspección muestra una observación narrativa bilingüe.

### 11.2 Fotografía

- Solo funciona con cámara equipada y abierta.
- Busca primero impacto físico y luego evidencia dentro del encuadre central.
- Una evidencia registrada no puede duplicarse.
- La fotografía, nombre y descripción se guardan de forma persistente.

### 11.3 Revelación UV

- Cada mancha recibe exposición mientras permanece dentro del haz.
- La exposición aumenta gradualmente.
- Al dejar de apuntar, la marca pierde visibilidad después de un retraso.
- Varias manchas sobre un objeto se revelan de forma independiente.

### 11.4 Teléfono

- Se recoge con E y pasa al subinventario.
- Puede configurarse con desbloqueo directo o PIN de cuatro dígitos.
- Incluye mensajes y registro de llamadas.
- Abrir una aplicación genera comentario narrativo la primera vez.
- Mensajes y llamadas se registran como evidencias digitales separadas.

### 11.5 Conexiones

- Se seleccionan dos fotografías para crear una conexión.
- El sistema guarda la conexión.
- Si la relación está contemplada, el Operador explica su valor.
- Si no existe relación directa, el Operador advierte que la conexión agrega ruido.
- Las conexiones requeridas desbloquean conclusiones y memorias.

### 11.6 Persistencia

- Evidencias, fotos, notas, conexiones, conclusiones y posiciones de pizarra se guardan automáticamente.
- La escena del crimen restaura posición segura del jugador.
- La oficina inicia desde la entrada al abrir el juego.
- Las herramientas siempre comienzan con la mano.
- Eliminar datos reinicia progreso, tutoriales, introducción y memorias desbloqueadas.

## 12. Caso: La llave por dentro

### Premisa

Julián Herrera, arquitecto de 41 años, aparece muerto en la sala de su casa familiar. La puerta está cerrada desde adentro, hay un frasco de pastillas junto al cuerpo y un mensaje final enviado a su expareja, Sofía Roldán.

La resolución correcta demuestra que Julián fue intoxicado por Víctor Salas, vecino y antiguo contratista. Víctor alteró una bebida, envió un mensaje falso, colocó el frasco y simuló el cierre interno para desviar la investigación hacia Nicolás y Sofía.

### Personajes

**Julián Herrera:** Víctima. Detectó documentos sucesorios y comprobantes de obra adulterados.  
**Nicolás Herrera:** Hermano. Falsificó documentos para vender la casa, pero no cometió el asesinato.  
**Sofía Roldán:** Expareja. Discutió con Julián y conserva una llave antigua. Su presencia es circunstancial.  
**Víctor Salas:** Vecino y contratista. Culpable real. Conoce la casa y tenía un móvil económico.  
**Elena Herrera:** Madre de Julián y Nicolás. Su enfermedad y la propiedad originan el conflicto sucesorio.

## 13. Línea temporal real

**17:30:** Sofía llega y discute con Julián.  
**18:05:** Sofía se retira y escribe a una amiga desde el transporte.  
**18:20:** Nicolás llega para discutir la venta.  
**18:45:** Nicolás se retira; Julián continúa vivo.  
**19:10:** Víctor ingresa por la entrada lateral.  
**19:20:** Julián lo enfrenta por comprobantes falsificados.  
**19:35:** Víctor mezcla medicación triturada en una bebida.  
**19:50:** Julián comienza a descompensarse.  
**20:00:** Víctor envía el mensaje falso desde el teléfono.  
**20:08:** Coloca el frasco junto al cuerpo.  
**20:20:** Sale por la ventana lateral.  
**20:23:** Manipula la llave con hilo de nylon.

## 14. Estructura de niveles

### 14.1 Oficina / Hub

**Propósito:** Presentar la fantasía, entregar información, seleccionar memorias y organizar evidencia.  
**Objetivo del jugador:** Leer expediente, montar memoria, revisar pizarra y formular hipótesis.  
**Sistemas:** Monitor, expediente, visor, galería, libreta, pizarra, confirmaciones y opciones.  
**Enseñanza:** Marcadores señalan primero expediente, luego asiento, monitor y visor.

### 14.2 Memoria 01: Casa Herrera

**Espacios:** Sala de estar y cocina.  
**Propósito:** Presentar el supuesto suicidio y demostrar que el método pudo ser intoxicación.  
**Evidencias mínimas:** Frasco, teléfono, mensajes, llamadas, huella, azucarero y nota.  
**Herramientas enseñadas:** Inspección, cámara, teléfono y UV.  
**Conclusión provisional:** La escena pudo ser manipulada; el frasco no explica por sí solo la muerte.

### 14.3 Memoria 02: Oficina de Víctor Salas

**Estado:** Planificada; requiere construcción de escena y arte.  
**Desbloqueo:** Seis evidencias registradas en Memoria 01.  
**Propósito:** Introducir el móvil real y vincular a Víctor con falsificación, acceso y materiales de obra.  
**Entorno:** Oficina/taller pequeño con archivadores, escritorio, planos, muestras de materiales, botas, teléfono laboral y comprobantes.  
**Evidencias:** Facturas duplicadas, deuda, hilo de nylon, plano de acceso lateral, botas compatibles y contradicción horaria.  
**Puzzle:** Comparar dos facturas con mismo número y montos distintos; identificar firma copiada y fecha incompatible.  
**Conclusión:** Víctor tenía móvil, conocimiento del acceso y materiales compatibles con la manipulación.

### 14.4 Memoria 03: Estudio de Julián

**Estado:** Planificada.  
**Propósito:** Separar el delito de Nicolás del asesinato y confirmar la denuncia contra Víctor.  
**Evidencias:** Testamento original, documento sucesorio falsificado, correo al abogado, carpeta Obra Salas y coartada de Nicolás.

### 14.5 Memoria 04: Entrada de la casa

**Estado:** Planificada.  
**Propósito:** Resolver la habitación cerrada.  
**Evidencias:** Ventana lateral, hilo, marca en llave, polvo de obra y huella de botas.  
**Puzzle:** Ordenar la secuencia de salida y manipulación de la llave.

### 14.6 Panel final

El jugador selecciona culpable, método, móvil, manipulación y sospechosos descartados. La acusación requiere evidencia y conclusiones específicas; no se valida por cantidad total.

## 15. Situaciones interesantes variadas

### Situación 1: La escena demasiado perfecta

El jugador ve el frasco junto al cuerpo antes que cualquier otra pista. Puede fotografiarlo inmediatamente, pero el comentario del Operador señala que su posición parece diseñada para ser encontrada. La situación introduce desconfianza sobre la lectura inicial.

### Situación 2: Presencia no equivale a culpabilidad

El jugador encuentra una huella de Sofía en una copa. Al conectarla directamente con el mensaje final, el sistema advierte que ambas pruebas demuestran vínculo y presencia previa, pero no horario de muerte ni método.

### Situación 3: Una aplicación cambia el significado de otra

El jugador lee conversaciones largas de Julián y después abre el mensaje final. La diferencia de tono, longitud y puntuación transforma el teléfono de aparente confirmación de suicidio en evidencia de manipulación.

### Situación 4: Revelación gradual

El jugador dirige la luz UV sobre el azucarero. Primero aparece una mancha pequeña; al recorrer la superficie surgen puntos separados. Debe mantener el haz y explorar físicamente el objeto, no activar una revelación binaria.

### Situación 5: Método sin culpable

El jugador conecta azucarero, taza y medicación triturada. Desbloquea la conclusión “intoxicación mediante bebida”, pero ningún sospechoso queda identificado. La situación separa resolver cómo ocurrió de resolver quién lo hizo.

### Situación 6: Conexión incorrecta con respuesta

El jugador conecta una fotografía familiar con el azucarero. La línea se guarda, pero el Operador explica que no existe relación causal demostrable. El jugador aprende que organizar no significa validar.

### Situación 7: La oficina cambia después de investigar

Al volver con seis evidencias, el monitor anuncia una memoria nueva y el expediente incorpora una línea sobre Víctor. El hub comunica progreso narrativo sin una pantalla de recompensa separada.

### Situación 8: Dos facturas, un mismo número

En la oficina de Víctor, el jugador coloca dos comprobantes sobre una mesa de luz. Comparten numeración, pero muestran montos y fechas diferentes. Debe marcar tres discrepancias para registrar falsificación.

### Situación 9: Evidencia incriminatoria que también libera

El documento sucesorio confirma que Nicolás falsificó una firma. Más adelante, un ticket lo ubica fuera del barrio durante la muerte. El jugador debe sostener simultáneamente que cometió un delito y que no fue el asesino.

### Situación 10: El testimonio literalmente verdadero

Víctor afirma que no vio salir a nadie por la puerta principal. Al descubrir la ventana lateral, el jugador comprende que la frase puede ser verdadera y aun así estar diseñada para engañar.

### Situación 11: Relectura de una memoria anterior

Después de encontrar hilo de nivelación en la oficina de Víctor, el jugador vuelve a la casa. Un fragmento antes irrelevante junto a la puerta ahora puede registrarse como evidencia.

### Situación 12: Riesgo de acusación prematura

El panel permite intentar una acusación antes de reunir todos los componentes. El sistema no termina la partida: indica qué parte del argumento carece de sustento, sin revelar la respuesta correcta.

### Situación 13: Objeto cotidiano con valor temporal

Una taza húmeda y un trapo recién usado no identifican al culpable, pero permiten demostrar que alguien limpió después de preparar la bebida. El estado del objeto aporta secuencia temporal.

### Situación 14: Acceso reconstruido espacialmente

El jugador compara un plano de reforma con la posición de polvo y huellas. La solución requiere mirar el espacio y ordenar un recorrido, no seleccionar una respuesta textual.

### Situación 15: Cierre argumentado

La acusación correcta exige construir una cadena: intoxicación, comprobantes falsificados, acceso lateral, hilo de obra y huella compatible. Si falta un eslabón, la conclusión queda incompleta aunque el culpable seleccionado sea correcto.

## 16. Curva de enseñanza

### Fase 1: Rol y objetivo

La introducción establece al Operador forense. Un marcador identifica el expediente asignado. El jugador no recibe instrucciones de herramientas todavía.

### Fase 2: Oficina

El tutorial espera movimiento, lectura del expediente, asiento, monitor, montaje y visor. Cada línea se reproduce una sola vez y permanece visible entre 6 y 14 segundos.

### Fase 3: Memoria

La interfaz contextual muestra controles en el lateral izquierdo. Se enseña una herramienta cuando se equipa, no todas al entrar.

### Fase 4: Investigación autónoma

Después del primer registro, las ayudas se reducen a objetivo actual y pistas por inactividad. Las pistas aparecen después de 180 segundos sin progreso.

### Fase 5: Deducción

La pizarra responde a conexiones. El expediente se actualiza y el monitor comunica nuevos desbloqueos.

## 17. Interfaz y experiencia de usuario

- Textos narrativos por encima del HUD y debajo de pausa/confirmaciones.
- Subtítulos separados de mensajes de acción.
- Controles contextuales sin fondo en el lateral izquierdo.
- Teléfono vertical con navegación táctil, teclado y mouse.
- Galería y libreta en pestañas separadas.
- Evidencias con nombre, descripción, foto y procedencia.
- Pausa con resumen del expediente activo y opciones persistentes.
- Confirmación antes de salir, volver a oficina o borrar datos.
- Español e inglés seleccionables.
- Presets gráficos Bajo, Medio, Alto, Épico y Personalizado.

## 18. Dirección visual

La oficina debe sentirse funcional, institucional y analógica, con tecnología CRT y materiales usados. Las memorias pueden presentar pequeños errores digitales para recordar que son reconstrucciones, sin impedir leer objetos.

La paleta separa funciones:

- Dorado apagado: expediente, narrativa y objetivos.
- Verde frío: sistemas, terminal y evidencia digital.
- Rojo: conexiones y advertencias.
- Blanco cálido: texto principal y fotografías.

La iluminación prioriza lectura espacial y evidencia. Objetos interactivos no dependen solamente de brillo; utilizan retícula, marcador o contexto narrativo.

## 19. Sonido

### Canales

- Volumen maestro.
- Ambiente.
- Efectos.
- Voces/subtítulos.
- Interfaz.

### Feedback requerido

- Pasos lentos según superficie.
- Equipar y guardar herramientas.
- Obturador y enfoque de cámara.
- Recoger, inspeccionar y dejar objetos.
- Encendido y zumbido UV.
- Desbloqueo, navegación y notificaciones del teléfono.
- Conexión correcta, conexión dudosa y conclusión desbloqueada.
- Ambientes diferenciados para oficina, casa y taller de Víctor.

## 20. Rendimiento y accesibilidad

- Objetivo mínimo: 30 FPS en gráficos integrados de gama baja a 720p/Bajo.
- Objetivo recomendado: 60 FPS a 1080p/Medio.
- Iluminación horneada para entorno estático.
- Evitar búsquedas globales por frame.
- Raycasts ejecutados por interacción o con intervalos.
- Escala de render configurable entre 60% y 115%.
- Sombras, texturas, antialiasing, VSync y límite de FPS configurables.
- Subtítulos siempre disponibles para narrativa.
- Ayudas contextuales y textos de acción desactivables por separado.
- Controles reasignables.

## 21. Robustez

- El jugador conserva una posición segura para recuperarse de caídas.
- Colliders del entorno deben impedir atravesar paredes y subir a mobiliario no navegable.
- Objetos inspeccionados restauran transform y collider.
- Ninguna interfaz debe permitir interacción exterior mientras está abierta.
- Carga de escenas utiliza transición y autoguardado.
- Las fotografías sin sprite reciben una miniatura de respaldo.
- La última página del expediente no vuelve automáticamente a la primera.

## 22. Validación

Cada versión debe probarse con al menos tres personas que no conozcan el proyecto.

Preguntas de validación:

1. ¿Entendió durante los primeros tres minutos que su rol era investigar un crimen?
2. ¿Encontró el expediente sin ayuda verbal externa?
3. ¿Pudo explicar la diferencia entre libreta, galería y expediente?
4. ¿Descubrió cómo equipar y abrir la cámara?
5. ¿Comprendió que una conexión podía ser incorrecta?
6. ¿Identificó al menos dos explicaciones posibles para la muerte?
7. ¿Pudo regresar a la oficina y continuar sin repetir el tutorial?
8. ¿La acusación final se sintió consecuencia de la evidencia?

Métricas del prototipo:

- Tiempo hasta abrir expediente: máximo 90 segundos.
- Tiempo hasta registrar primera evidencia: máximo 5 minutos.
- Porcentaje que abre cámara sin ayuda externa: mínimo 80%.
- Porcentaje que comprende el rol forense: mínimo 90%.
- Errores bloqueantes: cero durante una sesión completa.

## 23. Estado actual y roadmap

### Implementado

- Oficina, monitor, expediente, visor y transición.
- Memoria F1-House con sala y cocina.
- Cámara, inspección, UV, teléfono y rueda de herramientas.
- Galería, libreta, pizarra, conexiones y persistencia.
- Tutorial por fases, narrativa inicial y UI contextual.
- Opciones gráficas, sonido, idioma y controles.

### Próximo hito

1. Construir F2-ContractorOffice.
2. Configurar seis evidencias de Víctor.
3. Implementar puzzle de facturas duplicadas.
4. Integrar F2 como segunda memoria del monitor.
5. Crear conclusión de móvil real.
6. Completar panel de acusación.
7. Añadir audio definitivo y mezcla.
8. Realizar prueba con usuarios y registrar resultados.

## 24. Riesgos

**Exceso de exposición:** Se mitiga separando fantasía, expediente y comentarios contextuales.  
**Demasiadas ayudas simultáneas:** Se mitiga con activación por fases y controles contextuales.  
**Evidencia sin función:** Cada evidencia debe aportar método, móvil, acceso, horario, manipulación o descarte.  
**Acusación por intuición:** Se requieren cadenas de evidencia y conclusiones.  
**Rendimiento en hardware bajo:** Presets, iluminación horneada, reducción de sombras y optimización de búsquedas.  
**Crecimiento de alcance:** El prototipo académico prioriza oficina, F1, F2 y acusación provisional antes de añadir memorias adicionales.

## 25. Criterio de completitud académica

El prototipo se considera completo cuando una persona que no conoce Archive: NULL puede iniciar una partida, comprender su rol, leer el expediente, ingresar en F1, registrar evidencia con al menos tres herramientas, volver a la oficina, conectar pistas, desbloquear F2 y formular una acusación provisional sin intervención del desarrollador y sin errores bloqueantes.
