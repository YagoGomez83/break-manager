# 05 — Preguntas abiertas

Estado: 🔴 bloquea una fase · 🟡 se puede avanzar con un supuesto · ✅ resuelta.

| # | Pregunta | Impacto | Supuesto actual | Bloquea | Estado |
|---|---|---|---|---|---|
| Q4 | ¿Cuál es la cobertura mínima de puestos por grupo? | RN-19, RN-24 | Configurable, sin valor por defecto | Fase 5 | 🟡 |
| Q6 | ¿Los operadores tienen un puesto habitual fijo (para proponer asignaciones)? | HU-12 | Sí; el supervisor ajusta al generar el plan | Fase 3 | 🟡 |
| Q7 | ¿El turno D (18–00) tiene algún tratamiento especial en informes por cruzar la medianoche? | Fecha operativa | Fecha operativa = fecha de inicio | — | 🟡 |
| Q8 | Plataforma de repositorio y CI: ¿GitHub, GitLab u otra? ¿On-premise o nube? | Pipeline de la Fase 1 | GitHub Actions | Fase 1 | 🟡 |
| Q9 | ¿Hay certificado de firma de código para el instalador? ¿Hay antivirus o EDR que deba aprobar el agente? | Distribución del agente | Sin firma en el piloto; excepción en el antivirus | Fase 7 | 🟡 |
| Q10 | Texto del aviso de privacidad y período de retención: ¿quién los redacta y aprueba? | ADR-0006, HU-02 | Texto provisorio en el piloto | Fase 2 (piloto) | 🔴 |
| Q11 | Integración con Milestone: versión de XProtect, ¿usuarios personales o compartidos?, ¿Basic o Windows? | ADR-0005 | Login propio | Fase 8 | 🟡 |
| Q12 | ¿Un supervisor ve solo su grupo o todos? ¿Hay supervisores suplentes que cubren otros grupos? | Autorización | Ve su grupo; Coordinación ve todos; suplencias asignables por el Admin | Fase 5 | 🟡 |
| Q13 | Recursos de la VM (CPU, RAM, disco) y sistema operativo | Dimensionamiento | 2 vCPU, 4 GB RAM, 50 GB | Fase 7 | 🟡 |
| Q15 | ¿Quién gestiona el esquema base: Coordinación, o cada supervisor parte de su propio esquema? | Permisos (RN-33) | Coordinación gestiona el base; los supervisores lo ajustan para su grupo | Fase 3 | 🟡 |

## Resueltas

| # | Pregunta | Respuesta |
|---|---|---|
| R1 | Umbral de ausencia automática | 3 min sin entrada de mouse (inactividad a los 2 min) |
| R2 | Tipos de descanso no planificado | Baño, Médico, Llamada, Falla técnica, Convocado por el supervisor |
| R3 | ¿Los operadores rotan de PC? | No en el día: puesto fijo, salvo relevos |
| R4 | Login / Milestone | Login propio ahora; Milestone más adelante |
| R5 | Notificación del control | Aviso al primer login, aceptado y registrado (sujeto a validación legal) |
| R6 | Significado de los códigos del cronograma | Confirmados (ver `02-dominio.md` §4) |
| R7 | Relevos | Existen cuando hay más operadores que puestos; el supervisor decide quién sale según la operativa |
| R8 | ¿El teclado cuenta como actividad? | No: solo el mouse (zoom y paneo) |
| R9 (ex Q1) | ¿La ola 4 empieza a las 12:40 y no a las 12:30? | Sí, era un error de la planilla: 12:40 |
| R10 (ex Q2) | ¿Los grupos A, B y D usan el mismo patrón? | Sí, por ahora el mismo esquema para todos; cada supervisor debe poder modificarlo (ADR-0009) |
| R11 (ex Q3) | ¿Los operadores de 911 (AT) cuentan? | No: atienden teléfonos, tienen sus propios descansos y supervisor. Fuera del alcance. |
| R13 (ex Q14) | ¿Cuántos puestos hay y cuándo hay relevistas? | 20 puestos. Los relevistas son el excedente de gente sobre los puestos habilitados (p. ej. 17 personas y 15 puestos → 2 relevistas). Es raro, pero pasa con puestos rotos o días con más gente. |
| R14 (ex Q5) | ¿Cuándo descansan los relevistas? | El esquema tiene 25 posiciones para 20 puestos: las 21–25 son de relevo. Cada relevista descansa con la ola de su posición; por fila, salen en el último descanso de cada ronda. |
| R12 | ¿Cómo se agrupan las salidas? | Por defecto por columna (el esquema actual); el supervisor puede cambiar a por fila o puesto por puesto |
