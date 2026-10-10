// Inventario verificable y exportación XLSX sin dependencias externas.
// Uso: node Tools/Export-GameChanges.js [ruta/a/git.exe]
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '..');
const git = process.argv[2] || 'git';
const runGit = (...args) => execFileSync(git, args, { cwd: root, encoding: 'utf8' }).trim();
const script = 'Assets/ArchiveNull/Scripts/';
// Área | cambio | comportamiento implementado | ruta relativa | alcance de revisión
const inventory = `
Movimiento|Movimiento corporal al caminar|La vista combina pasos, cadencia e inercia de giro en un pivote independiente.|Jugador/FirstPersonHeadBob.cs|Código y recorrido de editor; sensación corporal pendiente de evaluación del jugador.
Movimiento|Respiración en reposo|La cámara incorpora oscilación de posición e inclinación al respirar.|Jugador/FirstPersonHeadBob.cs|Código; intensidad subjetiva a evaluar jugando.
Movimiento|Intensidad corporal ajustable|La intensidad del movimiento se conserva como preferencia y disminuye al apuntar.|Jugador/FirstPersonHeadBob.cs|Código y opciones del juego.
Herramientas|Rueda de equipo ampliada|Permite seleccionar cámara, UV, revelador, trípode, linterna, marcadores y teléfono recogido.|Evidence/EvidenceCameraController.cs|Pruebas de editor de selección y disponibilidad.
Herramientas|Equipo restringido en Casa Herrera|Trípode y linterna blanca no aparecen en F1-House; sus accesos quedan bloqueados y UV continúa disponible.|Evidence/EvidenceCameraController.cs|Pruebas de editor de restricciones de F1-House.
Herramientas|Trípode vacío|El soporte empieza sin cámara ni luces montadas.|Evidence/TestingToolsController.cs|Pruebas de editor de soporte vacío y montaje.
Herramientas|Holograma de colocación orientable|La vista previa muestra posición y validez; Q/R y rueda permiten girarla antes de confirmar.|Evidence/TestingToolsController.cs|Pruebas de colocación y captura del holograma.
Herramientas|Montar y recuperar el equipo de la mano|La herramienta se traslada al soporte y no puede equiparse de nuevo hasta recuperarla.|Evidence/EvidenceCameraController.cs|Pruebas de propiedad de herramienta y montaje en editor.
Herramientas|Mover un trípode instalado|Se reposiciona el soporte conservando el equipo montado; cancelar conserva su colocación anterior.|Evidence/TestingToolsController.cs|Pruebas de movimiento y cancelación en editor.
Herramientas|Escala de equipo adaptada al jugador|El trípode y sus distancias de interacción se ajustan a la altura de la cámara en cada escena.|Evidence/TestingToolsController.cs|Pruebas de colocación con escala de F1-Office.
Herramientas|Pincel y polvo revelador|Mantener el clic aplica revelador sobre rastros compatibles.|Evidence/TestingToolsController.cs|Código y pruebas de revelado en el laboratorio.
Herramientas|Encendido del equipo montado|La herramienta del soporte puede encenderse y apagarse sin recuperarla.|Evidence/EvidenceCameraController.cs|Pruebas de alimentación del equipo montado.
Herramientas|Consultas físicas sin bloquearse con el jugador|Los rayos de colocación e interacción ignoran los colliders del operador.|Evidence/PlacementSurface.cs|Pruebas de colocación y recogida en casa y oficina.
Herramientas|Colocación libre frente a mallas grandes|La comprobación de penetración real evita rechazos por cajas envolventes de habitaciones.|Evidence/PlacementSurface.cs|Pruebas de apoyo libre en F1-House y F1-Office.
Evidencias|Marcadores amarillos numerados|Los tags se colocan con vista previa y se recogen con E.|Evidence/EvidenceMarkerPlacement.cs|Pruebas de colocación y recogida en ambas memorias.
Evidencias|Renumeración consecutiva de tags|Recoger un marcador cierra el hueco en la numeración de los restantes.|Evidence/EvidenceMarkerPlacement.cs|Prueba de numeración en editor.
Evidencias|Guardado de posiciones de marcadores|Las posiciones y el orden de los tags se incluyen en los datos de partida.|Core/GameSaveSystem.cs|Código de serialización y prueba de persistencia de tags.
Cámara|Cámara física con pantalla propia|El equipo sostenido presenta una pantalla integrada con la imagen de una lente física.|Evidence/PhysicalEvidenceCamera.cs|Pruebas de lente física y captura del LCD.
Cámara|Fotografía desde la lente del equipo|El encuadre procede de la cámara del objeto, también cuando está sobre el trípode.|Evidence/PhysicalEvidenceCamera.cs|Pruebas del origen de lente y encuadre montado.
Cámara|Pantalla apagada al bajar la cámara|El LCD se muestra negro cuando la cámara deja de estar encendida para fotografiar.|Evidence/PhysicalEvidenceCamera.cs|Prueba de apagado del LCD en editor.
Cámara|Interfaz de cámara en el LCD|La pantalla incorpora indicadores de encuadre, enfoque, zoom y exposición.|Evidence/EvidenceCameraController.cs|Código y captura del LCD; diseño visual evaluable en juego.
Cámara|Flash y obturador al fotografiar|La captura incluye una respuesta visual de flash y cierre del obturador.|Evidence/EvidenceCameraController.cs|Código y recorrido de fotografía en editor.
Cámara|Álbum con descarte de fotografías|El jugador puede conservar o eliminar las fotografías de su investigación.|Evidence/PhotoAlbum.cs|Código del álbum; inventario disponible desde TAB.
Memorias|Carga asíncrona con progreso real|La pantalla negra sigue la operación de carga de Unity y confirma la activación antes del 100%.|SceneFlow/MemoryLoadingScreen.cs|Pruebas del flujo de carga en editor; progreso suavizado, sin tareas ficticias.
Memorias|Negro completo antes de cargar|El fundido y una espera con frames garantizan que la memoria no aparezca antes del apagado.|SceneFlow/MemoryLoadingScreen.cs|Prueba de intervalo negro antes de cargar.
Memorias|Reconstrucción visible tras la carga|La expansión empieza después de terminar la carga y retirar los fundidos negros.|SceneFlow/SceneRebuildTransition.cs|Prueba y captura del inicio de reconstrucción de F1-House.
Memorias|Radio explorable fijo|El límite de la memoria permanece anclado y no crece al acercarse el jugador.|SceneFlow/OfficeDissolveTransition.cs|Código y pruebas del límite; radios documentados en MechanicsLab.md.
Memorias|Reconstrucción más lenta y cierre más breve|La expansión tiene un mínimo de 4,5 segundos; el cierre usa el 65% de esa duración.|SceneFlow/OfficeDissolveTransition.cs|Código y recorrido de transiciones en editor.
Memorias|Retraer la memoria hacia el jugador|La onda de salida utiliza la posición del operador como destino del cierre.|SceneFlow/OfficeDissolveTransition.cs|Prueba del origen de retracción en editor.
Memorias|Cielo negro durante la reconstrucción|La memoria parte del vacío y recupera su skybox original al completarse.|SceneFlow/SceneRebuildTransition.cs|Código y prueba de inicio de memoria.
Memorias|Píxeles de límite animados en GPU|El borde reutiliza una malla de 1536 quads para animar los píxeles.|SceneFlow/MemoryBoundaryPixels.cs|Código; no se certifica un mínimo universal de 60 FPS.
Memorias|Mensajes específicos al salir|La carga distingue cerrar memoria, descargar entorno y recuperar la oficina.|SceneFlow/MemoryLoadingScreen.cs|Código y recorrido completo de regreso al menú.
Memorias|Regreso a la oficina con recursos conservados|Una caché mantiene dependencias de la oficina entre visitas a memorias.|SceneFlow/OfficeAssetCache.cs|El tiempo del último regreso se registra como MEASURE en editor-mechanics.txt; depende del equipo y del estado de la caché.
Memorias|Precarga al iniciar directamente en memoria|Un manifiesto prepara de forma asíncrona los recursos de la oficina real.|SceneFlow/OfficeAssetManifest.cs|Código y recurso OfficeWarmup.asset; salir antes de acabar puede requerir espera.
Interfaz|Pausa con fondo desenfocado|La pausa usa una captura del mundo, navegación superior y acciones con separadores finos.|Menu/Pausa/GlobalPauseMenu.cs|Captura de pausa y pruebas en editor; valoración estética pendiente del jugador.
Interfaz|Desenfoque calculado al pausar|El fondo captura y procesa la escena al abrir la pausa sin renderizar continuamente.|UI/PauseBackdrop.cs|Código del fondo y captura de pausa.
Interfaz|Controles según herramienta equipada|Los controles cambian con el equipo y se ocultan con la mano vacía.|UI/ContextControlHints.cs|Código y pruebas de interfaz contextual.
Interfaz|Confirmación de sesión rediseñada|Una tarjeta permite confirmar o cancelar la entrada a memoria.|UI/RuntimeConfirmationDialog.cs|Captura y pruebas de confirmación de sesión.
Interfaz|Visor con información de sesión|El visor presenta estado y acciones de entrada antes de cargar la memoria.|Office/VRHeadsetArchiveStarter.cs|Captura del visor y prueba de sesión.
Interfaz|Subtítulos y ayudas redistribuidos|La guía separa objetivos, avisos de evidencia y subtítulos para reducir superposiciones.|Evidence/EvidenceGuidanceController.cs|Código y capturas; legibilidad final a evaluar en las resoluciones de uso.
Escritorio|Archive OS sustituye la presentación CRT|El ordenador abre un escritorio moderno y retira los efectos retro activos.|Office/CRTMainMenuController.Desktop.cs|Prueba de apertura y captura modern-desktop.png.
Escritorio|Inicio de sesión con carga|El escritorio muestra un arranque antes de habilitar las aplicaciones.|Office/ModernOfficeDesktop.cs|Prueba de arranque; animación de sistema, no barra de carga de escena.
Escritorio|Aplicaciones y barra de tareas|Expedientes, Memorias, Informe final y Configuración se abren desde el escritorio.|Office/ModernOfficeDesktop.cs|Pruebas de aplicaciones y configuración en editor.
Escritorio|Ventanas y carpetas interactuables|Las ventanas pueden moverse, minimizarse y cerrarse; las carpetas permiten preparar memorias.|Office/ModernOfficeDesktop.cs|Pruebas de navegación y captura memory-folders.png.
Escritorio|Escritorio dentro del monitor físico|Un Canvas de espacio de mundo se adapta a la superficie existente de la pantalla sin cubrir la cámara del jugador.|Office/ModernOfficeDesktop.cs|Compilación y prueba del modo y anclaje del Canvas en editor.
Escritorio|Cursor limitado a la pantalla del ordenador|La posición del puntero se transforma a coordenadas de la pantalla física y se limita a sus bordes durante el foco.|Office/ModernOfficeDesktop.cs|Pruebas de confinamiento y raycasts sobre botones del monitor.
Escritorio|Sesión visible al apartarse del ordenador|Salir de la vista desactiva los clics y mantiene encendido el escritorio con la ventana que estaba abierta.|Office/ModernOfficeDesktop.cs|Prueba de conservación de ventana y liberación de raycasts.
Escritorio|Animaciones de las ventanas|Abrir, cerrar y minimizar combina fundido, escala y desplazamiento con curvas suavizadas; la barra restaura la ventana.|Office/ModernOfficeDesktop.cs|Pruebas de finalización de apertura, minimización, restauración y cierre.
Escritorio|Renderers de UI garantizados|El controlador repara CanvasRenderer faltantes en Graphics serializados antes de aplicar el tema; los elementos nuevos lo crean explícitamente.|Office/CRTMainMenuController.cs|Compilación y comprobación de CanvasRenderer de todos los Graphics del escritorio.
Memorias|Suspender clics mientras se descarga la escena|La carga suspende raycasters del entorno saliente y restaura los que sobreviven al completar la transición.|SceneFlow/MemoryLoadingScreen.cs|37 comprobaciones aprobadas y cero fallos de runtime en la regresión del monitor; desaparece el error TitleText durante la descarga.
Expediente|Retratos de los cuatro personajes|El atlas proporciona retratos de Julián, Nicolás, Sofía y Víctor.|Office/CasePortraits.cs|Recurso de personajes ficticios y captura primary-case-file.png.
Expediente|Reabrir información primaria desde el escritorio|El lector permite consultar el mismo expediente desde su carpeta y conserva proporciones de retratos.|Office/CaseFileReader.cs|Prueba de apertura desde Archive OS.
Tutorial|Leer el expediente antes de usar el ordenador|Sentarse y abrir el ordenador requieren descubrir la información primaria en la primera sesión.|Office/OfficeOnboarding.cs|Pruebas del bloqueo inicial y avance tras leer.
Tutorial|Guía inicial de cinco pasos|La ayuda conduce por expediente, asiento, escritorio, memoria y visor.|Office/OfficeOnboarding.cs|Pruebas del flujo inicial; se oculta con otros paneles abiertos.
Tutorial|Eliminar las dos pantallas de presentación|El inicio ya no instala las tarjetas antiguas de presentación.|Narrative/DetectiveFantasyIntro.cs|Código y recorrido desde MainMenu.
Progresión|Requisitos claros para habilitar F1-Office|La carpeta indica nota del estante, mensajes y llamadas; la huella sola no habilita la memoria.|Evidence/CaseEvidenceCatalog.cs|Pruebas de requisitos y captura de carpetas.
Progresión|Reparar desbloqueos de partidas anteriores|El índice de memorias se sincroniza con el desbloqueo guardado sin repetir el aviso.|Evidence/EvidenceDrivenMemoryUnlock.cs|Prueba de reparación de desbloqueo persistido.
Validación|Aislar partidas usadas en pruebas|El argumento del editor fuerza guardados de laboratorio incluso al comenzar en la oficina.|Core/GameSaveSystem.cs|Código y recorrido final con aislamiento.
Validación|Laboratorio de mecánicas en editor|Una escena permite probar colocación, fotografía, revelado y obstáculos.|Assets/ArchiveNull/Editor/MechanicsLabEditor.cs|Recorrido de laboratorio en Play Mode sin build de ejecutable.
Validación|Recorrido automático ampliado|El probe permite el recorrido completo o la validación específica del monitor, tutorial, progresión y herramientas de oficina.|Assets/ArchiveNull/Editor/MechanicsEditorProbe.cs|Resultado del último recorrido en la hoja Resumen y en editor-mechanics.txt; el alcance depende del modo ejecutado.
Validación|Prueba de oficina independiente|El comando OfficeOnly inicia el recorrido del editor en MainMenu.|Tools/Test-EditorMechanics.ps1|Revisión del argumento y entrada de editor; guardados aislados.
`.trim().split('\n').map((line, index) => {
  const [area, title, behavior, source, validation] = line.split('|');
  const file = source.startsWith('Assets/') || source.startsWith('Tools/') ? source : script + source;
  if (!fs.existsSync(path.join(root, file))) throw new Error('Ruta inexistente: ' + file);
  return { id: index + 1, area, title, behavior, file, validation };
});
if (inventory.length < 30) throw new Error('El inventario requiere al menos 30 cambios.');
const logPath = 'Artifacts/EditorValidation/editor-mechanics.txt';
const log = fs.readFileSync(path.join(root, logPath), 'utf8');
const passed = (log.match(/^PASS /gm) || []).length;
const failed = (log.match(/^FAIL /gm) || []).length;
const revision = runGit('rev-parse', '--short', 'HEAD');
const localDate = new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Argentina/Buenos_Aires' }).format(new Date());
const commits = runGit('log', '-12', '--format=%h%x09%s%x09%b%x1e').split('\x1e').map(s => s.trim()).filter(Boolean).map(s => s.split('\t'));
const latest = new Map();
for (const item of inventory) {
  if (!latest.has(item.file)) latest.set(item.file, runGit('log', '-1', '--format=%h', '--', item.file));
}
const sheets = [
  { name: 'Resumen', widths: [38, 112], rows: [
    ['ARCHIVE: NULL', 'Inventario de cambios importantes'],
    ['Cambios documentados', inventory.length],
    ['Fecha del inventario', localDate],
    ['Revisión consultada', revision],
    ['Alcance', 'Inventario de implementaciones presentes. No es una reconstrucción de fechas históricas ni de autorías.'],
    ['Validación registrada', `${passed} PASS y ${failed} FAIL en el último informe del editor.`],
    ['Fuente de resultados', logPath],
    ['Referencia de Git por cambio', 'La columna indica el último commit del archivo consultado, no necesariamente el commit de origen de cada comportamiento.'],
    ['Commits recientes', `La hoja Commits muestra los ${commits.length} commits locales más recientes anteriores a esta exportación.`],
    ['Límites', 'Las pruebas automatizadas cubren sus comprobaciones concretas. No certifican percepción visual, sensación corporal ni 60 FPS en todos los equipos.'],
    ['Validación sin ejecutables', 'El juego se verifica en Play Mode. No se generó una build para elaborar este inventario.'],
    ['Regenerar', 'node Tools/Export-GameChanges.js ruta/a/git.exe'],
    ['Preferencia de trabajo', 'Un commit por cambio pequeño y coherente; título y descripción en español. Regla persistida en AGENTS.md.']
  ] },
  { name: 'Cambios', widths: [7, 18, 46, 80, 90, 76, 23], rows: [
    ['ID', 'Área', 'Cambio importante', 'Comportamiento implementado', 'Archivo de implementación', 'Validación y límites', 'Último commit del archivo'],
    ...inventory.map(c => [c.id, c.area, c.title, c.behavior, c.file, c.validation, latest.get(c.file)])
  ] },
  { name: 'Commits', widths: [15, 82, 125], rows: [
    ['Commit local', 'Título en español', 'Descripción y verificación'], ...commits
  ] }
];
const esc = value => String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
function column(n) { let result = ''; do { result = String.fromCharCode(65 + n % 26) + result; n = Math.floor(n / 26) - 1; } while (n >= 0); return result; }
function worksheet(sheet) {
  const rows = sheet.rows.map((row, index) => {
    const cells = row.map((value, col) => {
      const ref = column(col) + (index + 1);
      const style = index === 0 ? 1 : index % 2 ? 2 : 3;
      return typeof value === 'number' ? `<c r="${ref}" s="${style}"><v>${value}</v></c>` : `<c r="${ref}" s="${style}" t="inlineStr"><is><t xml:space="preserve">${esc(value)}</t></is></c>`;
    }).join('');
    return `<row r="${index + 1}" ht="${index === 0 ? 32 : sheet.name === 'Commits' ? 100 : 64}" customHeight="1">${cells}</row>`;
  }).join('');
  const cols = sheet.widths.map((width, index) => `<col min="${index + 1}" max="${index + 1}" width="${width}" customWidth="1"/>`).join('');
  const end = column(sheet.widths.length - 1) + sheet.rows.length;
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:${end}"/><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/><selection pane="bottomLeft" activeCell="A2" sqref="A2"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight="64"/><cols>${cols}</cols><sheetData>${rows}</sheetData>${sheet.name === 'Resumen' ? '' : `<autoFilter ref="A1:${end}"/>`}<pageMargins left="0.3" right="0.3" top="0.5" bottom="0.5" header="0.3" footer="0.3"/><pageSetup orientation="landscape" paperSize="9"/></worksheet>`;
}
const entries = new Map();
entries.set('[Content_Types].xml', `<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>${sheets.map((_, i) => `<Override PartName="/xl/worksheets/sheet${i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>`).join('')}</Types>`);
entries.set('_rels/.rels', '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>');
entries.set('xl/workbook.xml', `<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><bookViews><workbookView/></bookViews><sheets>${sheets.map((s, i) => `<sheet name="${s.name}" sheetId="${i + 1}" r:id="rId${i + 1}"/>`).join('')}</sheets></workbook>`);
entries.set('xl/_rels/workbook.xml.rels', `<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">${sheets.map((_, i) => `<Relationship Id="rId${i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${i + 1}.xml"/>`).join('')}<Relationship Id="rId${sheets.length + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>`);
entries.set('xl/styles.xml', `<?xml version="1.0" encoding="UTF-8"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/><color rgb="FF20334A"/></font><font><b/><sz val="12"/><name val="Calibri"/><color rgb="FFFFFFFF"/></font></fonts><fills count="4"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF173552"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFEEF4FA"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="3" borderId="0" xfId="0" applyFill="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>`);
sheets.forEach((s, i) => entries.set(`xl/worksheets/sheet${i + 1}.xml`, worksheet(s)));
// ZIP con CRC32 y compresión DEFLATE, formato estándar de los documentos OOXML.
function crc32(bytes) {
  let crc = 0xffffffff;
  for (const byte of bytes) { crc ^= byte; for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0); }
  return (crc ^ 0xffffffff) >>> 0;
}
const files = [], directory = [];
let offset = 0;
for (const [name, xml] of entries) {
  const filename = Buffer.from(name), content = Buffer.from(xml), compressed = zlib.deflateRawSync(content), crc = crc32(content);
  const header = Buffer.alloc(30);
  header.writeUInt32LE(0x04034b50); header.writeUInt16LE(20, 4); header.writeUInt16LE(8, 8); header.writeUInt16LE(33, 12);
  header.writeUInt32LE(crc, 14); header.writeUInt32LE(compressed.length, 18); header.writeUInt32LE(content.length, 22); header.writeUInt16LE(filename.length, 26);
  files.push(header, filename, compressed);
  const central = Buffer.alloc(46);
  central.writeUInt32LE(0x02014b50); central.writeUInt16LE(20, 4); central.writeUInt16LE(20, 6); central.writeUInt16LE(8, 10); central.writeUInt16LE(33, 14);
  central.writeUInt32LE(crc, 16); central.writeUInt32LE(compressed.length, 20); central.writeUInt32LE(content.length, 24); central.writeUInt16LE(filename.length, 28); central.writeUInt32LE(offset, 42);
  directory.push(central, filename); offset += header.length + filename.length + compressed.length;
}
const central = Buffer.concat(directory), end = Buffer.alloc(22);
end.writeUInt32LE(0x06054b50); end.writeUInt16LE(entries.size, 8); end.writeUInt16LE(entries.size, 10); end.writeUInt32LE(central.length, 12); end.writeUInt32LE(offset, 16);
const output = path.join(root, 'Docs/Cambios_Importantes_Archive_NULL.xlsx');
fs.writeFileSync(output, Buffer.concat([...files, central, end]));
console.log(`Excel creado: ${inventory.length} cambios, ${sheets.length} hojas, ${passed} PASS / ${failed} FAIL registrados.`);
