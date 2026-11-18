# 00 — Visión del producto

## Problema

En el centro de monitoreo, los descansos de los operadores se planifican en planillas
(cronograma mensual de guardias + cronograma de descansos por puesto). Hoy:

- No hay registro confiable de **cuándo** sale y vuelve realmente cada operador.
- No se detecta de forma objetiva cuándo un puesto queda **sin atención** (operador inactivo o ausente).
- La supervisión no tiene una vista en vivo de **cobertura de puestos** frente al mínimo requerido.
- Coordinación y Calidad arman sus informes a mano, cruzando planillas.

## Objetivos

| # | Objetivo | Cómo se mide |
|---|---|---|
| O1 | Planificar los descansos del turno a partir del cronograma de guardias y del esquema de descansos por olas de puestos | Plan diario generado sin intervención manual para el 100 % de los operadores de guardia |
| O2 | Avisar al operador cada descanso planificado y registrar inicio y fin reales | ≥ 95 % de los descansos con inicio y fin registrados |
| O3 | Detectar inactividad del mouse y ausencias no declaradas | 100 % de las inactividades > 2 min registradas |
| O4 | Dar a la supervisión un tablero en vivo de operadores y puestos | Latencia evento → tablero < 2 s en la LAN |
| O5 | Entregar a Coordinación y Calidad información ya procesada (adherencia, ausencias, inactividad) | Informes exportables sin cruce manual de planillas |

## Usuarios

> Alcance: **operadores de monitoreo** (sección AO del cronograma), 20 puestos, entre 15 y 22 operadores de guardia por turno.
> Los operadores de **911** quedan fuera: atienden teléfonos, tienen sus propios descansos y su propio supervisor.

- **Operador:** usa el agente de escritorio. Recibe avisos, inicia y finaliza descansos y declara descansos no planificados.
- **Supervisor (uno por grupo):** monitorea el tablero en vivo, clasifica inactividades, reclasifica ausencias, ajusta el esquema de descansos de su grupo y el plan del día, y asigna relevos.
- **Coordinador:** consulta informes y gestiona planificación (cronogramas, esquema base de descansos).
- **Calidad:** consulta informes y auditoría (reclasificaciones, adherencia).
- **Administrador:** usuarios, puestos (PCs), parámetros y catálogos.

## Alcance

### Incluido (versión 1)

- Importación del cronograma mensual de guardias desde Excel (grupos A–D, solo operadores de monitoreo).
- Esquema base de descansos por "olas" de puestos (hoy común a los 4 grupos), con distribución flexible (por columna, por fila o puesto por puesto) que cada supervisor puede ajustar para su grupo, con versiones y auditoría.
- Generación y ajuste diario del plan de descansos, incluidos los puestos habilitados y los relevistas.
- Agente Windows: login, avisos, inicio y fin de descanso, descansos no planificados, detección de inactividad del mouse, aviso de privacidad versionado, cola offline.
- Web: tablero en vivo, clasificación y reclasificación auditada, alertas de cobertura mínima, informes y exportación.
- Despliegue on-premise en una VM Linux con Docker Compose, TLS interno, backups y observabilidad básica.

### Excluido (por ahora)

- Integración con el login de Milestone XProtect (diferida, ADR-0005).
- Detección de simuladores de actividad ("mouse jigglers") por correlación con Milestone.
- Operadores de 911 (equipo AT) y grupo de Analítica.
- App móvil, acceso desde fuera de la LAN, notificaciones por mail o mensajería.
- Liquidación de sueldos o integración con RRHH.

## Restricciones

- Todas las PCs de operador son Windows, están encendidas 24 h y **no hay Active Directory**.
- El servidor es una VM **on-premise**, accesible solo desde la LAN.
- Los controles de actividad deben ser **conocidos** por los trabajadores (art. 71 LCT, ADR-0006).
- Mantenimiento por un equipo chico: se prioriza simplicidad (monolito modular, un solo lenguaje).
