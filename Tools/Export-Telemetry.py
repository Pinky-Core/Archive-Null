"""Offline JSONL -> Spanish HTML + Excel-compatible CSV. Python standard library only."""
import argparse
import csv
import html
import json
import math
import re
import statistics
from collections import Counter, defaultdict
from pathlib import Path


def number(row, key):
    value = row.get(key, 0)
    return float(value) if isinstance(value, (float, int)) and math.isfinite(value) and value >= 0 else 0.0


def median(values):
    return round(statistics.median(values), 2) if values else None


def percentile(values, fraction):
    if not values:
        return None
    values = sorted(values)
    index = (len(values) - 1) * fraction
    lo, hi = math.floor(index), math.ceil(index)
    return round(values[lo] + (values[hi] - values[lo]) * (index - lo), 2)


def read_events(folder, include_tests=False):
    rows, warnings, seen, excluded = [], [], {}, 0
    for file in sorted(Path(folder).rglob('*.jsonl')):
        with file.open(encoding='utf-8-sig') as source:
            for line_index, line in enumerate(source, 1):
                try:
                    row = json.loads(line)
                    if not isinstance(row, dict) or row.get('schema') != 1:
                        raise ValueError('esquema desconocido')
                    if not row.get('sessionId') or not row.get('runId') or not row.get('type') or not isinstance(row.get('sequence'), int):
                        raise ValueError('identificadores incompletos')
                    if row.get('test') and not include_tests:
                        excluded += 1
                        continue
                    key = row['sessionId'], row['sequence']
                    if key in seen:
                        if seen[key] != row:
                            warnings.append(f'{file.name}:{line_index}: duplicado con contenido diferente; se conserva el primero')
                        continue
                    seen[key] = row
                    rows.append(row)
                except (ValueError, TypeError) as error:
                    warnings.append(f'{file.name}:{line_index}: {error}')
    return sorted(rows, key=lambda e: (e.get('utc', ''), e['sessionId'], e['sequence'])), warnings, excluded


def catalog_from_repo():
    path = Path(__file__).resolve().parents[1] / 'Assets/ArchiveNull/Scripts/Evidence/CaseEvidenceCatalog.cs'
    if not path.exists():
        return {}
    return {id_: {'name': name, 'scene': 'F1-House' if int(n) <= 13 else 'F1-Office'}
            for n, id_, name in re.findall(r'new\((\d+), "([^"]+)", "([^"]+)"', path.read_text(encoding='utf-8-sig'))}


def analyze(events):
    runs, sessions = defaultdict(list), defaultdict(list)
    catalog = catalog_from_repo()
    for e in events:
        runs[e['runId']].append(e)
        sessions[e['sessionId']].append(e)
        if e['type'] == 'catalog':
            catalog[e['id']] = {'name': e.get('label') or e['id'], 'scene': e.get('context', '')}
        elif e['type'] == 'evidence_collected' and e['id'] not in catalog:
            catalog[e['id']] = {'name': e.get('label') or e['id'], 'scene': e.get('context') or e.get('scene', '')}
    full = {id_ for id_, group in runs.items() if not any(e.get('partial') for e in group)
            and any(e['type'] == 'run_started' or (e['type'] == 'run_resumed' and e.get('context') == 'new_tracking') for e in group)}
    # Restrict discovery metrics to runs measured from their beginning. Restored progress is a separate cohort.
    run_rows, completed_times = [], []
    for id_, group in runs.items():
        collected = [e for e in group if e['type'] == 'evidence_collected']
        completed = next((e for e in group if e['type'] == 'case_completed'), None)
        reset = any(e['type'] == 'run_reset' for e in group)
        active = max(number(e, 'runActive') for e in group)
        elapsed = max(number(e, 'runElapsed') for e in group)
        if completed and id_ in full:
            completed_times.append(number(completed, 'runActive'))
        run_rows.append({'caso': id_, 'cobertura': 'desde cero' if id_ in full else 'progreso previo',
                         'resultado': 'resuelto' if completed else 'reiniciado' if reset else 'sin resolución observada',
                         'sesiones': len({e['sessionId'] for e in group}), 'segundos_activos': round(active, 2),
                         'segundos_observados': round(elapsed, 2),
                         'resolucion_segundos': round(number(completed, 'runActive'), 2) if completed else None,
                         'pistas_nuevas': len({e['id'] for e in collected}),
                         'pistas_previas': len({e['id'] for e in group if e['type'] == 'evidence_baseline'}),
                         'intentos_informe': sum(e['type'] == 'report_submitted' for e in group),
                         'ayudas': sum(e['type'] == 'hint_requested' for e in group),
                         'orden_pistas': ' > '.join(dict.fromkeys(e['id'] for e in collected))})
    evidence_rows = []
    for id_, data in catalog.items():
        eligible = {rid for rid in full if any(e['type'] == 'scene_enter' and e['id'] == data['scene'] for e in runs[rid])}
        found, times, scene_times = set(), [], []
        all_collects = [e for e in events if e['type'] == 'evidence_collected' and e.get('id') == id_]
        for rid in full:
            first = next((e for e in runs[rid] if e['type'] == 'evidence_collected' and e['id'] == id_), None)
            if first:
                found.add(rid)
                times.append(number(first, 'runActive'))
                entries = [e for e in runs[rid] if e['type'] == 'scene_enter' and e['id'] == data['scene'] and number(e, 'runActive') <= number(first, 'runActive')]
                if entries:
                    # Relative to the first entry, so return visits count toward the search time.
                    scene_times.append(number(first, 'runActive') - min(number(e, 'runActive') for e in entries))
        inspections = sum(e['type'] == 'inspection_started' and e.get('id') == id_ for e in events)
        evidence_rows.append({'id': id_, 'pista': data['name'], 'escena': data['scene'], 'casos_desde_cero': len(full),
                              'casos_que_visitaron_escena': len(eligible), 'casos_que_recogieron': len(found),
                              'recogida_por_visita_pct': round(100 * len(found & eligible) / len(eligible), 1) if eligible else None,
                              'recogida_por_caso_pct': round(100 * len(found) / len(full), 1) if full else None,
                              'mediana_segundos_hasta_pista': median(times), 'p90_segundos_hasta_pista': percentile(times, .9),
                              'mediana_desde_primera_visita': median(scene_times), 'registros_todas_coberturas': len(all_collects),
                              'inspecciones_todas_coberturas': inspections,
                              'ayudas_todas_coberturas': sum(e['type'] == 'hint_requested' and e.get('id') == id_ for e in events)})
    evidence_rows.sort(key=lambda row: (row['recogida_por_visita_pct'] is None, -(row['recogida_por_visita_pct'] or 0), row['id']))
    session_rows, scene_rows, routes, gaps, activity, tools = [], [], [], [], Counter(), defaultdict(float)
    for sid, group in sessions.items():
        group.sort(key=lambda e: e['sequence'])
        session_rows.append({'sesion': sid, 'casos': len({e['runId'] for e in group}),
                             'inicio_utc': group[0].get('utc'), 'ultimo_evento_utc': group[-1].get('utc'),
                             'segundos_activos': round(max(number(e, 'sessionActive') for e in group), 2),
                             'segundos_observados': round(max(number(e, 'sessionElapsed') for e in group), 2),
                             'cierre_normal': any(e['type'] == 'session_ended' for e in group), 'eventos': len(group)})
        current_scene, visit_start, tool, last_collection = '', None, None, None
        for index, e in enumerate(group):
            prev = group[index - 1] if index else e
            dt = max(0, number(e, 'sessionElapsed') - number(prev, 'sessionElapsed'))
            if prev['type'] == 'activity':
                activity_state = prev.get('context', '')
            elif index == 0:
                activity_state = 'sin clasificar'
            activity[activity_state] += dt
            if tool and e.get('scene') == prev.get('scene') and e['runId'] == prev['runId']:
                tools[tool] += max(0, number(e, 'sessionActive') - number(prev, 'sessionActive'))
            if e['type'] == 'tool_equipped':
                tool = e['id']
            if e['type'] == 'scene_enter':
                current_scene, visit_start, tool = e['id'], e, None
                routes.append({'sesion': sid, 'caso': e['runId'], 'escena': current_scene, 'segundo_activo': round(number(e, 'runActive'), 2)})
            if visit_start and (e['type'] in ('scene_exit', 'session_ended', 'run_reset') or index == len(group) - 1):
                scene_rows.append({'sesion': sid, 'caso': visit_start['runId'], 'escena': current_scene,
                                   'segundos_activos': round(max(0, number(e, 'sessionActive') - number(visit_start, 'sessionActive')), 2),
                                   'segundos_observados': round(max(0, number(e, 'sessionElapsed') - number(visit_start, 'sessionElapsed')), 2)})
                visit_start = None
            if e['type'] == 'evidence_collected':
                if last_collection and last_collection['runId'] == e['runId']:
                    gaps.append({'caso': e['runId'], 'desde': last_collection['id'], 'hasta': e['id'],
                                 'segundos_activos_entre_pistas': round(max(0, number(e, 'runActive') - number(last_collection, 'runActive')), 2)})
                last_collection = e
    attempts = [{'caso': e['runId'], 'tipo': e['type'], 'id': e.get('id'), 'resultado': e.get('value'),
                 'campos_informe_incorrectos': e.get('relatedId') if e['type'] == 'report_submitted' else '',
                 'secuencia_reconstruccion': e.get('context') if e['type'] == 'reconstruction_attempt' else '',
                 'segundo_activo': round(number(e, 'runActive'), 2)} for e in events
                if e['type'] in ('report_submitted', 'reconstruction_attempt', 'reconstruction_solved', 'timeline_attempt', 'phone_pin_attempt')]
    action_counts = Counter((e['type'], e.get('id', ''), e.get('relatedId', ''), e.get('value', 0)) for e in events if e['type'] not in ('catalog', 'heartbeat'))
    actions = [{'accion': key[0], 'id': key[1], 'relacion': key[2], 'valor': key[3], 'cantidad': count} for key, count in action_counts.most_common()]
    positions = [{'sesion': e['sessionId'], 'caso': e['runId'], 'escena': e.get('scene'), 'estado': e.get('context'),
                  'segundo_activo': round(number(e, 'runActive'), 2), 'x': e.get('x'), 'y': e.get('y'), 'z': e.get('z')} for e in events if e['type'] == 'heartbeat']
    performance = [{'sesion': e['sessionId'], 'escena': e.get('scene'), 'estado': e.get('context'),
                    'fps_medio_intervalo': round(number(e, 'fps'), 1), 'peor_frame_ms': round(number(e, 'maxFrameMs'), 1),
                    'memoria_administrada_mb': round(number(e, 'managedMemoryMb'), 1)} for e in events if e['type'] == 'heartbeat']
    summary = {'sesiones': len(sessions), 'casos': len(runs), 'casos_desde_cero': len(full), 'casos_con_progreso_previo': len(runs) - len(full),
               'resueltos_desde_cero': len(completed_times), 'mediana_resolucion_segundos': median(completed_times),
               'p90_resolucion_segundos': percentile(completed_times, .9), 'min_resolucion_segundos': min(completed_times) if completed_times else None,
               'max_resolucion_segundos': max(completed_times) if completed_times else None,
               'tiempo_por_estado_segundos': {k: round(v, 2) for k, v in activity.items()}}
    return summary, {'pistas': evidence_rows, 'casos': run_rows, 'sesiones': session_rows, 'visitas': scene_rows,
                     'recorridos': routes, 'intervalos_pistas': gaps, 'intentos': attempts, 'acciones': actions,
                     'posiciones': positions, 'rendimiento': performance,
                     'herramientas': [{'herramienta': k, 'segundos_activos_equipada': round(v, 2)} for k, v in tools.items()]}


def csv_file(path, rows):
    with path.open('w', encoding='utf-8-sig', newline='') as target:
        if not rows:
            return
        writer = csv.DictWriter(target, fieldnames=list(rows[0]), delimiter=';')
        writer.writeheader()
        for row in rows:
            # Avoid spreadsheet formula interpretation of imported identifiers.
            writer.writerow({k: "'" + v if isinstance(v, str) and v.startswith(('=', '+', '-', '@')) else v for k, v in row.items()})


def table(rows):
    if not rows:
        return '<p>Sin datos todavía.</p>'
    esc = lambda value: html.escape('—' if value is None else str(value))
    return '<div class="scroll"><table><thead><tr>' + ''.join('<th>' + esc(k.replace('_', ' ')) + '</th>' for k in rows[0]) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join('<td>' + esc(v) + '</td>' for v in row.values()) + '</tr>' for row in rows) + '</tbody></table></div>'


def export(source, output, include_tests=False):
    events, warnings, excluded = read_events(source, include_tests)
    summary, tables = analyze(events)
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    for name, rows in tables.items():
        csv_file(output / (name + '.csv'), rows)
    (output / 'resumen.json').write_text(json.dumps({**summary, 'eventos_prueba_excluidos': excluded, 'advertencias': warnings, 'incluye_pruebas': include_tests}, ensure_ascii=False, indent=2), encoding='utf-8')
    cards = [('Sesiones', summary['sesiones']), ('Casos desde cero', summary['casos_desde_cero']),
             ('Casos con progreso previo', summary['casos_con_progreso_previo']), ('Resueltos desde cero', summary['resueltos_desde_cero']),
             ('Mediana hasta resolver (min)', round(summary['mediana_resolucion_segundos'] / 60, 1) if summary['mediana_resolucion_segundos'] is not None else 'Sin casos resueltos')]
    bars = ''.join('<div class="bar"><span>' + html.escape(row['pista']) + '</span><i style="width:' + str(row['recogida_por_visita_pct'] or 0) + '%"></i><b>' + (str(row['recogida_por_visita_pct']) + '%' if row['recogida_por_visita_pct'] is not None else 'Sin visitas') + f" · {row['casos_que_recogieron']}/{row['casos_que_visitaron_escena']}</b></div>" for row in tables['pistas'])
    sections = [('Pistas: recogida y dificultad de búsqueda', 'pistas'), ('Tiempo y resultado por caso', 'casos'),
                ('Tiempo por visita a cada escena', 'visitas'), ('Intentos, PIN y errores del informe', 'intentos'),
                ('Herramientas: tiempo equipado observado', 'herramientas'), ('Acciones: fotos, descartes, conexiones, audios y ayudas', 'acciones'),
                ('Intervalos entre pistas', 'intervalos_pistas'), ('Orden de escenas y revisitas', 'recorridos'), ('Sesiones y cierres', 'sesiones'),
                ('Rendimiento por intervalo observado', 'rendimiento')]
    body = '<h1>Telemetría de ARCHIVE: NULL</h1><p>Informe local de partidas observadas. ' + ('<strong>INCLUYE PRUEBAS: no son estadísticas de jugadores.</strong>' if include_tests else f'Pruebas excluidas: {excluded} eventos.') + '</p>'
    body += '<div class="cards">' + ''.join('<article><small>' + title + '</small><strong>' + str(value) + '</strong></article>' for title, value in cards) + '</div>'
    body += '<p>La frecuencia principal usa casos medidos desde cero y muestra recogidas / casos que visitaron la escena. Las pistas nunca recogidas también aparecen. Los guardados previos están separados; no equivalen a una partida nueva.</p><h2>Qué pistas se recogen más y menos</h2>' + bars
    body += '<h2>Cómo interpretar estos datos</h2><p>Tiempo activo excluye pausa, carga, aplicación sin foco y ausencia de entrada durante más de 120 s; leer e inspeccionar cuenta. El tiempo observado incluye esas esperas, pero no el juego cerrado. El tiempo activo limita los saltos de un frame a 1 s para no contar bloqueos como investigación. Los tiempos del caso continúan con el guardado y pueden perder hasta el último autoguardado tras un cierre inesperado. Una sesión sin cierre o un caso sin resolver no demuestra abandono. No hay identificación de personas: varios casos pueden pertenecer a la misma persona.</p>'
    body += '<p>La mediana y el P90 de resolución usan únicamente casos completos resueltos. Una tasa baja puede indicar que no se alcanzó la escena: compará ambos porcentajes en pistas.csv. Los intervalos desde la primera visita incluyen actividades fuera de esa escena. Los estados se estiman entre eventos; las posiciones se muestrean cada 10 s y se redondean a 0,5 unidades del mapa. El tiempo equipado no demuestra uso efectivo. Los campos del informe se numeran 0–4 en el orden de la ficha. Datos insuficientes se muestran con —, nunca como un éxito o fallo supuesto.</p>'
    body += '<h2>Distribución del tiempo observado</h2>' + table([{'estado': k, 'segundos': v} for k, v in summary['tiempo_por_estado_segundos'].items()])
    for title, name in sections:
        body += '<h2>' + title + '</h2><p><a href="' + name + '.csv">Abrir CSV en Excel</a></p>' + table(tables[name])
    body += '<h2>Recorridos espaciales</h2><p><a href="posiciones.csv">Posiciones y tiempos muestreados</a>. Cada punto representa una observación, no una trayectoria continua. Solo se grafican muestras activas; las escalas son unidades de cada escena. Rendimiento: FPS medios del intervalo, peor frame y memoria administrada; no es memoria total de GPU ni una garantía de FPS.</p>'
    for scene in sorted({p['escena'] for p in tables['posiciones'] if p['escena']}):
        points = [p for p in tables['posiciones'] if p['escena'] == scene and p['estado'] == 'active']
        if not points:
            continue
        xs, zs = [float(p['x'] or 0) for p in points], [float(p['z'] or 0) for p in points]
        x0, z0 = min(xs), min(zs)
        dx, dz = max(1, max(xs) - x0), max(1, max(zs) - z0)
        body += '<h3>' + html.escape(scene) + '</h3><svg role="img" aria-label="Muestras activas del recorrido" viewBox="0 0 800 340" style="background:#1d2d38;max-width:900px;width:100%">'
        for x, z in zip(xs, zs):
            body += f'<circle cx="{30+(x-x0)/dx*740:.1f}" cy="{310-(z-z0)/dz*280:.1f}" r="5" fill="#89e7d3" opacity=".3"><title>x={x}, z={z}</title></circle>'
        body += f'<text x="20" y="330" fill="white">X: {min(xs):.1f} … {max(xs):.1f} · Z: {min(zs):.1f} … {max(zs):.1f}</text></svg>'
    body += '<h2>Calidad del registro</h2>' + table([{'advertencia': warning} for warning in warnings])
    style = 'body{margin:0 auto;padding:40px;max-width:1500px;background:#101820;color:#e4edf2;font:16px system-ui}h1,h2{color:#89e7d3}h2{margin-top:48px}.cards{display:flex;gap:16px;flex-wrap:wrap}article{background:#1d2d38;padding:22px;border-radius:12px;min-width:180px}strong{display:block;font-size:28px}small{display:block}p{line-height:1.6;color:#c3d0db}.scroll{overflow:auto}table{border-collapse:collapse;width:100%;font-size:13px}td,th{padding:12px;text-align:left;border-bottom:1px solid #314452;white-space:nowrap}th{color:#89e7d3}a{color:#f0ca7e}.bar{position:relative;margin:12px 0;background:#1d2d38;padding:12px;min-height:24px}.bar span,.bar b{position:relative;z-index:1}.bar b{float:right}.bar i{position:absolute;inset:0 auto 0 0;background:#247163;opacity:.55}'
    (output / 'index.html').write_text('<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Telemetría · ARCHIVE: NULL</title><style>' + style + '</style><body>' + body + '</body></html>', encoding='utf-8')
    return summary


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--include-tests', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(args.input, args.output, args.include_tests), ensure_ascii=False, indent=2))
