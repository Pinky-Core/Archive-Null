# DATOS SIMULADOS — no son partidas reales

Esta carpeta contiene una muestra ficticia para demostrar informes, completar una maqueta de planilla o probar importaciones. **No sirve como evidencia de testing ni para concluir qué pistas encuentran jugadores reales.**

Incluye 20 casos inventados: 16 medidos desde cero y 4 con progreso previo, con sesiones reanudadas, pistas encontradas y omitidas, informes incorrectos y correctos, ayudas, fotografías, herramientas y muestras ficticias de rendimiento. Las fechas del año 2000 son artificiales. Los tiempos y frecuencias son decisiones del generador, no observaciones.

- **`Telemetria_SIMULADA.xlsx`**: libro listo para importar en Google Sheets, con eventos y tablas de casos, pistas, etapas, intentos y otras métricas. La primera pestaña explica el origen simulado.
- **`Eventos_SIMULADOS.csv`**: eventos en columnas compatibles con el receptor, más una columna `origen=SIMULADO`. Los IDs empiezan por `demo_`, la versión es `DEMO-SIMULADA` y todos tienen `test=TRUE`.
- **`Informe_SIMULADO/index.html`**: informe visual con ranking de pistas, tiempos y tablas. Todo el conjunto es una demostración.
- **`JSONL_SIMULADOS/demo.jsonl`**: datos originales ficticios, que el exportador normal excluye por tener `test=true`.
- **`SIMULADO_resumen.json`**: cantidades y comprobaciones del generador.

## Importar a Google Sheets

1. Crear una planilla nueva llamada **Archive NULL — DEMO SIMULADA**.
2. Elegir **Archivo → Importar → Subir** y seleccionar `Telemetria_SIMULADA.xlsx`.
3. Importar como **hojas nuevas**. Mantener los nombres con `SIMULADO` y la pestaña `LEER_PRIMERO`.

También se puede importar el CSV como una hoja nueva. No reemplazar la pestaña `Eventos` de la planilla de partidas reales. El generador no contacta Apps Script ni envía datos a Google.

## Regenerar

Desde la raíz del proyecto, con Python 3.10 o posterior:

```powershell
python Tools/Generate-DemoTelemetry.py
```

La semilla fija `42017` hace reproducible la demostración. El generador comprueba IDs únicos, secuencias y tiempos por sesión, cobertura de pistas necesarias en los casos resueltos y exclusión total de esta muestra al analizar sin incluir pruebas.
