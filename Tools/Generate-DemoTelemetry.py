"""Generate explicitly fictional playtest data. Never sends data to a receiver."""
import argparse
import csv
import importlib.util
import json
import random
import zipfile
from datetime import datetime, timedelta, timezone
from pathlib import Path
from xml.sax.saxutils import escape

spec = importlib.util.spec_from_file_location('telemetry', Path(__file__).with_name('Export-Telemetry.py'))
telemetry = importlib.util.module_from_spec(spec)
spec.loader.exec_module(telemetry)

FIELDS = ('schema sequence utc sessionId runId type scene id relatedId label context version test partial value '
          'sessionActive sessionElapsed runActive runElapsed x y z fps maxFrameMs managedMemoryMb').split()
LINKS = [('f1_house_huella-zapato', 'f1_office_Huellas'), ('f1_office_Huellas', 'f1_ofice_llave'),
         ('f1_house_cup', 'f1_house_ornamentsmall_01'), ('f1_house_ornamentsmall_01', 'f1_office_tarro_pastillas'),
         ('f1_house_cube', 'f1_ofice_factura1'), ('f1_ofice_llave', 'f1_house_huella-zapato'),
         ('phone_messages', 'phone_call_log'), ('f1_house_comprobante', 'phone_call_log'),
         ('f1_house_documento', 'f1_house_comprobante')]
REQUIRED = {id_ for pair in LINKS for id_ in pair}


class FictionalRun:
    def __init__(self, number, rng, catalog):
        self.number, self.rng, self.catalog = number, rng, catalog
        self.run_id = f'demo_case_{number:02}'
        self.partial = number > 16
        self.solved = number <= 12 or number in (17, 18)
        self.pace = rng.uniform(.65, 1.5)
        self.active = self.elapsed = 0
        self.scene, self.found, self.rows = 'MainMenu', set(), []
        self.phone_unlocked = False
        self.session_number = 0
        self.start_session()

    def start_session(self):
        self.session_number += 1
        self.session_id = f'demo_session_{self.number:02}_{self.session_number}'
        self.base_active, self.base_elapsed, self.sequence = self.active, self.elapsed, 0
        # Deliberately fictional historical dates, not dates of real testing.
        self.clock = datetime(2000, 1, 1, tzinfo=timezone.utc) + timedelta(days=self.number * 2 + self.session_number - 1)
        self.scene = 'MainMenu'
        self.emit('session_started', context='simulated_demo')
        self.emit('run_resumed', context='new_tracking' if self.session_number == 1 and not self.partial else 'saved_progress')
        for id_, data in self.catalog.items():
            self.emit('catalog', id_, label=data['name'], context=data['scene'])
        for id_ in sorted(self.found):
            self.emit('evidence_baseline', id_)
        if self.partial and self.session_number == 1:
            for id_ in ('f1_house_documento', 'f1_house_cube', 'f1_house_phone'):
                self.found.add(id_)
                self.emit('evidence_baseline', id_)
        self.emit('scene_enter', self.scene)
        self.emit('activity', context='active')

    def emit(self, type_, id_='', related='', label='', context='', value=0):
        self.sequence += 1
        self.rows.append(dict(schema=1, sequence=self.sequence,
                              utc=(self.clock + timedelta(seconds=self.elapsed - self.base_elapsed)).isoformat(),
                              sessionId=self.session_id, runId=self.run_id, type=type_, scene=self.scene, id=id_,
                              relatedId=related, label=label, context=context, value=value,
                              version='DEMO-SIMULADA', test=True, partial=self.partial,
                              sessionActive=self.active-self.base_active, sessionElapsed=self.elapsed-self.base_elapsed,
                              runActive=self.active, runElapsed=self.elapsed,
                              x=0, y=0, z=0, fps=0, maxFrameMs=0, managedMemoryMb=0, dataset='SIMULADO'))

    def wait(self, seconds):
        while seconds > 0:
            step = min(45, seconds)
            self.active += step * self.pace
            self.elapsed += step * self.pace
            seconds -= step
            self.emit('heartbeat', context='active')
            self.rows[-1].update(x=round(self.rng.uniform(-8, 8)*2)/2, z=round(self.rng.uniform(-5, 5)*2)/2,
                                 y=1.7, fps=round(self.rng.uniform(48, 72), 1),
                                 maxFrameMs=round(self.rng.uniform(17, 43), 1), managedMemoryMb=self.rng.randint(360, 540))

    def visit(self, scene):
        self.emit('scene_exit', self.scene)
        self.emit('activity', context='loading')
        self.elapsed += self.rng.randint(8, 18)
        self.scene = scene
        self.emit('scene_enter', scene)
        self.emit('activity', context='active')

    def collect(self, id_):
        if id_ in self.found:
            return
        if id_ in ('phone_messages', 'phone_call_log'):
            if 'f1_house_phone' not in self.found:
                self.collect('f1_house_phone')
            self.unlock_phone()
            self.emit('phone_screen', 'Chat' if id_ == 'phone_messages' else 'Calls')
        self.wait(self.rng.randint(25, 100))
        if self.rng.random() < .23:
            self.emit('hint_requested', id_)
        self.emit('inspection_started', id_)
        self.wait(self.rng.randint(5, 18))
        self.emit('inspection_ended')
        if id_ in ('f1_house_ornamentsmall_01', 'f1_office_Huellas'):
            self.emit('trace_revealed', id_, context='Ultraviolet' if id_.startswith('f1_house') else 'Powder')
        self.emit('photo_taken', f'demo_photo_{self.number}_{len(self.found)}')
        if self.rng.random() < .18:
            self.emit('photo_deleted', f'demo_photo_{self.number}_{len(self.found)}')
        self.emit('evidence_collected', id_, label=self.catalog[id_]['name'], context=self.catalog[id_]['scene'])
        self.found.add(id_)

    def unlock_phone(self):
        if self.phone_unlocked:
            return
        self.emit('tool_equipped', 'InventoryItem')
        self.emit('phone_pin_attempt', value=0)
        self.wait(self.rng.randint(20, 55))
        self.emit('phone_pin_attempt', value=1)
        self.phone_unlocked = True
        self.emit('voice_started', 'phone_julian', value=12552)
        self.wait(13)

    def play(self):
        self.wait(self.rng.randint(60, 150))
        self.visit('F1-House')
        self.emit('tool_equipped', 'Camera')
        house = [id_ for id_, data in self.catalog.items() if data['scene'] == 'F1-House']
        self.rng.shuffle(house)
        reaches_office = self.solved or self.number % 2 == 0
        for id_ in house:
            mandatory = self.solved and id_ in REQUIRED or reaches_office and id_ in ('f1_house_phone', 'phone_messages', 'phone_call_log')
            if mandatory or self.rng.random() < (.78 if self.solved else .42):
                self.collect(id_)
        if 'f1_house_phone' in self.found:
            self.unlock_phone()
        self.visit('MainMenu')
        if self.number % 3 == 0:
            self.emit('session_ended')
            self.start_session()
        if reaches_office:
            assert {'phone_messages', 'phone_call_log'} <= self.found
            self.wait(self.rng.randint(25, 60))
            self.visit('F1-Office')
            self.emit('tool_equipped', 'Powder')
            for id_, data in self.catalog.items():
                if data['scene'] == 'F1-Office' and (self.solved or self.rng.random() < .6):
                    self.collect(id_)
            self.visit('MainMenu')
            self.wait(self.rng.randint(150, 400))
            for a, b in LINKS:
                if a in self.found and b in self.found:
                    self.emit('connection_created', a, b, value=1)
                    self.wait(self.rng.randint(12, 35))
            self.emit('reconstruction_attempt', context='0,2,1,3,4')
            self.wait(self.rng.randint(40, 120))
        for _ in range(self.rng.randint(1, 3) if reaches_office else 0):
            self.emit('report_submitted', related='1,3', value=self.rng.choice((20, 40, 60, 80)))
            self.wait(self.rng.randint(40, 130))
        if self.solved:
            assert REQUIRED <= self.found
            self.emit('reconstruction_attempt', context='0,1,2,3,4')
            self.emit('reconstruction_solved', 'door_reconstruction')
            self.wait(self.rng.randint(40, 90))
            self.emit('report_submitted', value=100)
            self.emit('case_completed')
        else:
            self.wait(self.rng.randint(40, 150))
            if self.number == 15:
                self.emit('run_reset')
        self.emit('session_ended')
        return self.rows


def write_workbook(output, sheets):
    """Small standards-based XLSX; no third-party package or formulas."""
    def column(index):
        text = ''
        while index:
            index, remainder = divmod(index-1, 26)
            text = chr(65+remainder)+text
        return text
    def cell(ref, value):
        if isinstance(value, bool):
            return f'<c r="{ref}" t="b"><v>{int(value)}</v></c>'
        if isinstance(value, (int, float)):
            return f'<c r="{ref}"><v>{value}</v></c>'
        return f'<c r="{ref}" t="inlineStr"><is><t xml:space="preserve">{escape("" if value is None else str(value))}</t></is></c>'
    ns = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    relationships = 'http://schemas.openxmlformats.org/package/2006/relationships'
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
        types = ['<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>',
                 '<Default Extension="xml" ContentType="application/xml"/>',
                 '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>']
        book, rels = [], []
        for index, (name, rows) in enumerate(sheets.items(), 1):
            data = []
            for row_index, row in enumerate(rows, 1):
                data.append(f'<row r="{row_index}">'+''.join(cell(f'{column(i)}{row_index}', value) for i, value in enumerate(row, 1))+'</row>')
            archive.writestr(f'xl/worksheets/sheet{index}.xml', f'<worksheet xmlns="{ns}"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" state="frozen"/></sheetView></sheetViews><sheetData>'+''.join(data)+'</sheetData></worksheet>')
            book.append(f'<sheet name="{escape(name)}" sheetId="{index}" r:id="rId{index}"/>')
            rels.append(f'<Relationship Id="rId{index}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{index}.xml"/>')
            types.append(f'<Override PartName="/xl/worksheets/sheet{index}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>')
        archive.writestr('[Content_Types].xml', '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'+''.join(types)+'</Types>')
        archive.writestr('_rels/.rels', f'<Relationships xmlns="{relationships}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>')
        archive.writestr('xl/workbook.xml', f'<workbook xmlns="{ns}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>'+''.join(book)+'</sheets></workbook>')
        archive.writestr('xl/_rels/workbook.xml.rels', f'<Relationships xmlns="{relationships}">'+''.join(rels)+'</Relationships>')


def generate(output):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    catalog = telemetry.catalog_from_repo()
    rng = random.Random(42017)
    events = [event for n in range(1, 21) for event in FictionalRun(n, rng, catalog).play()]
    assert all(e['test'] and e['version'] == 'DEMO-SIMULADA' for e in events)
    assert len({(e['sessionId'], e['sequence']) for e in events}) == len(events)
    for session in {e['sessionId'] for e in events}:
        group = [e for e in events if e['sessionId'] == session]
        assert [e['sequence'] for e in group] == list(range(1, len(group)+1))
        assert all(b['sessionActive'] >= a['sessionActive'] for a, b in zip(group, group[1:]))
    journal_dir = output / 'JSONL_SIMULADOS'
    journal_dir.mkdir(exist_ok=True)
    (journal_dir / 'demo.jsonl').write_text(''.join(json.dumps(e, ensure_ascii=False)+'\n' for e in events), encoding='utf-8')
    normal, warnings, excluded = telemetry.read_events(journal_dir)
    assert not normal and not warnings and excluded == len(events)
    summary = telemetry.export(journal_dir, output / 'Informe_SIMULADO', include_tests=True)
    assert summary['casos'] == 20 and summary['casos_desde_cero'] == 16 and summary['casos_con_progreso_previo'] == 4
    assert summary['resueltos_desde_cero'] == 12
    _, tables = telemetry.analyze(events)
    sheets = {'LEER_PRIMERO': [['DATOS SIMULADOS — NO SON PARTIDAS REALES'],
                               ['20 casos ficticios: 16 desde cero y 4 con progreso previo.'],
                               ['Fechas artificiales del año 2000; semilla reproducible 42017.'],
                               ['No usar como resultados de testing o conclusiones sobre jugadores.'],
                               ['Importar en una planilla de demostración separada.']]}
    with (output / 'Eventos_SIMULADOS.csv').open('w', encoding='utf-8-sig', newline='') as target:
        writer = csv.writer(target)
        header = ['eventoUnico', 'recibidoUTC', *FIELDS, 'origen']
        writer.writerow(header)
        raw_rows = []
        for e in events:
            row = [e['sessionId']+':'+str(e['sequence']), e['utc'], *[e[k] for k in FIELDS], 'SIMULADO']
            raw_rows.append(row)
            writer.writerow(['TRUE' if v is True else 'FALSE' if v is False else v for v in row])
    sheets['Eventos_SIMULADOS'] = [header, *raw_rows]
    for name, rows in tables.items():
        marked = [{'origen': 'SIMULADO', **row} for row in rows]
        telemetry.csv_file(output / 'Informe_SIMULADO' / (name+'.csv'), marked)
        if marked:
            sheets[name[:20]+'_SIMULADO'] = [list(marked[0]), *[list(row.values()) for row in marked]]
    write_workbook(output / 'Telemetria_SIMULADA.xlsx', sheets)
    report = output / 'Informe_SIMULADO/index.html'
    text = report.read_text(encoding='utf-8').replace('<h1>Telemetría de ARCHIVE: NULL</h1>',
        '<h1>DATOS SIMULADOS · ARCHIVE: NULL</h1><p><strong>Muestra ficticia de demostración. No son partidas reales ni resultados de testing.</strong></p>')
    report.write_text(text, encoding='utf-8')
    summary_file = output / 'Informe_SIMULADO/resumen.json'
    summary_file.write_text(json.dumps({'origen': 'SIMULADO', 'nota': 'Datos ficticios; no son resultados de jugadores.',
                                       **json.loads(summary_file.read_text(encoding='utf-8'))}, ensure_ascii=False, indent=2), encoding='utf-8')
    manifest = {'origen': 'SIMULADO', 'semilla': 42017, 'eventos': len(events), **summary,
                'validacion': 'IDs únicos; secuencias y tiempos monotónicos; todos los eventos test=true; exclusión completa del informe normal.'}
    (output / 'SIMULADO_resumen.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Docs/TelemetryDemo')
    generate(parser.parse_args().output)
