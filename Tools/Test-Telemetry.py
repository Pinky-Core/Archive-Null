"""Synthetic fixtures only: never treat these results as player statistics."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('telemetry', Path(__file__).with_name('Export-Telemetry.py'))
telemetry = importlib.util.module_from_spec(spec)
spec.loader.exec_module(telemetry)


def event(type_, run='a', session='s', time=0, **fields):
    return dict(schema=1, sequence=fields.pop('sequence', 1), type=type_, runId=run, sessionId=session,
                utc='2026-01-01T00:00:00Z', sessionActive=time, sessionElapsed=time, runActive=time,
                runElapsed=time, id=fields.pop('id', ''), partial=False, **fields)


class TelemetryTests(unittest.TestCase):
    def test_no_sample_has_no_rates(self):
        summary, tables = telemetry.analyze([])
        self.assertEqual(summary['casos'], 0)
        self.assertIsNone(summary['mediana_resolucion_segundos'])
        self.assertEqual(len(tables['pistas']), 17)
        self.assertTrue(all(e['recogida_por_visita_pct'] is None for e in tables['pistas']))

    def test_visited_denominator_and_never_found(self):
        rows = [event('run_started'), event('scene_enter', id='F1-House'),
                event('evidence_collected', id='f1_house_cube', time=30),
                event('run_started', run='b', session='t')]
        _, tables = telemetry.analyze(rows)
        note = next(row for row in tables['pistas'] if row['id'] == 'f1_house_cube')
        self.assertEqual(note['recogida_por_visita_pct'], 100)
        self.assertEqual(note['recogida_por_caso_pct'], 50)
        self.assertEqual(note['mediana_segundos_hasta_pista'], 30)
        cup = next(row for row in tables['pistas'] if row['id'] == 'f1_house_cup')
        self.assertEqual(cup['recogida_por_visita_pct'], 0)

    def test_partial_and_missing_start_are_separate(self):
        a = event('case_completed', time=10)
        b = event('run_started', run='b', session='t')
        b['partial'] = True
        summary, _ = telemetry.analyze([a, b])
        self.assertEqual(summary['casos_con_progreso_previo'], 2)
        self.assertEqual(summary['resueltos_desde_cero'], 0)

    def test_resolution_across_sessions_counts_case_once(self):
        summary, _ = telemetry.analyze([event('run_started'), event('session_ended', time=100),
                                        event('run_resumed', session='t', time=100, context='saved_progress'),
                                        event('case_completed', session='t', time=150)])
        self.assertEqual(summary['sesiones'], 2)
        self.assertEqual(summary['resueltos_desde_cero'], 1)
        self.assertEqual(summary['mediana_resolucion_segundos'], 150)

    def test_duplicate_import_tests_and_truncated_last_line(self):
        with tempfile.TemporaryDirectory() as name:
            root = Path(name)
            a = event('run_started')
            b = event('session_started', session='test', test=True)
            (root / 'one.jsonl').write_text(json.dumps(a)+'\n'+json.dumps(b)+'\n{"broken":', encoding='utf-8')
            (root / 'copy.jsonl').write_text(json.dumps(a)+'\n', encoding='utf-8')
            rows, warnings, excluded = telemetry.read_events(root)
            self.assertEqual(len(rows), 1)
            self.assertEqual(excluded, 1)
            self.assertEqual(len(warnings), 1)

    def test_reset_splits_cases(self):
        summary, tables = telemetry.analyze([event('run_started'), event('run_reset', time=20),
                                             event('run_started', run='b', time=0)])
        self.assertEqual(summary['casos'], 2)
        self.assertEqual(tables['casos'][0]['resultado'], 'reiniciado')
        self.assertEqual(tables['casos'][1]['resultado'], 'sin resolución observada')

    def test_scene_dwell_and_tool_duration(self):
        _, tables = telemetry.analyze([event('run_started', sequence=1), event('scene_enter', id='F1-House', sequence=2),
                                       event('tool_equipped', id='Camera', time=5, sequence=3, scene='F1-House'),
                                       event('scene_exit', time=20, sequence=4, scene='F1-House'),
                                       event('session_ended', time=22, sequence=5, scene='MainMenu')])
        self.assertEqual(tables['visitas'][0]['segundos_activos'], 20)
        self.assertEqual(tables['herramientas'][0]['segundos_activos_equipada'], 15)

    def test_safe_csv_and_html(self):
        with tempfile.TemporaryDirectory() as name:
            root = Path(name)
            source = root / 'source'
            source.mkdir()
            (source / 'one.jsonl').write_text(json.dumps(event('run_started', id='=1+1', label='<script>')), encoding='utf-8')
            telemetry.export(source, root / 'report')
            self.assertIn("'=1+1", (root / 'report/acciones.csv').read_text(encoding='utf-8-sig'))
            self.assertNotIn('<script>', (root / 'report/index.html').read_text(encoding='utf-8'))
            self.assertTrue((root / 'report/resumen.json').exists())


if __name__ == '__main__':
    unittest.main()
