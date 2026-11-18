# ADR-0009 — Esquemas de descanso flexibles por grupo y niveles de ajuste

- **Estado:** Aceptado
- **Fecha:** 2026-11-18
- **Amplía:** ADR-0008

## Contexto

- Hoy los 4 grupos usan el mismo esquema (el del grupo C: 5 olas por columna, 6 descansos, 70 min).
- El negocio exige que **cada supervisor pueda modificar los descansos de su grupo por razones operativas**:
  - Que salgan por columna, por fila o puesto por puesto.
  - Cambiar en qué ola descansa cada relevista.
  - Ajustar horarios.
- Hay ajustes **permanentes** (el grupo D prefiere salir por fila) y ajustes **de un día** (hoy falta gente, hay un evento).

## Decisión

1. **Separar "cuándo" de "quién":** ("quién" = posiciones, sean puestos o relevos)
   - El esquema tiene **olas con franjas** (los horarios).
   - Aparte, tiene una **distribución** (`WaveLayout`) que asigna puestos a olas: `ByColumn` (por defecto), `ByRow` o `Custom`.
   - Cambiar la distribución no toca los horarios, y al revés.
2. **Cuatro niveles de ajuste con precedencia** (el de abajo gana sobre el de arriba, sin modificarlo):

   | Nivel | Quién | Duración | Ejemplo |
   |---|---|---|---|
   | Esquema base | Coordinación | Hasta nueva versión | Las 5 olas del grupo C |
   | Esquema del grupo | Supervisor del grupo | Hasta nueva versión | El grupo D sale por fila |
   | Ajuste del día (plan del turno) | Supervisor del grupo | Solo esa fecha | Hoy el puesto 7 sale con la ola 3 |
   | Reprogramación en vivo | Supervisor del grupo | Solo ese descanso | Posponer 15 min por un evento |

3. **Copia al personalizar, no herencia viva:**
   - El esquema del grupo nace como copia de la versión vigente del base.
   - Si el base cambia, el supervisor recibe un aviso y decide si incorpora el cambio.
   - Así un cambio de Coordinación no altera por sorpresa la operativa de un grupo.
4. **Versiones inmutables con vigencia y motivo obligatorio:** cada cambio crea una versión. Los planes publicados quedan atados a la versión con la que se generaron. Todo queda en la auditoría.
5. **Mismas validaciones en todos los niveles:**
   - Bloqueantes: franjas dentro del turno, sin solapes en una ola, todo puesto en exactamente una ola.
   - Advertencias: simultaneidad y cobertura mínima.
   - El supervisor **ve la cobertura resultante antes de confirmar**.
6. **El relevo es parte de la distribución:**
   - El esquema tiene 25 posiciones: puestos 1–20 y relevos 21–25. Los relevistas descansan con la ola de su posición; no hace falta una regla aparte.
   - Por columna, sale un relevista con cada ola. Por fila, los cinco salen juntos en la última ola de cada ronda.
   - El sistema sugiere qué relevista cubre qué puesto en cada franja, y el supervisor lo puede cambiar.

## Alternativas consideradas

| Alternativa | Ventajas | Desventajas |
|---|---|---|
| Un esquema fijo; solo reprogramación en vivo | Muy simple | El supervisor repetiría el mismo ajuste todos los días; no refleja la operativa real |
| Esquema del grupo con herencia viva del base (solo se guardan las diferencias) | Los cambios del base se propagan solos | Comportamiento difícil de predecir; un cambio central altera grupos ya ajustados |
| Edición libre de horarios por operador, sin olas | Máxima flexibilidad | Se pierde el modelo de olas, que es como planifica el negocio; validar la cobertura se vuelve más difícil |
| **Esquema base + copia por grupo + ajuste diario + reprogramación** | Flexible, predecible y auditable | Más pantallas que un esquema fijo (aceptable: es el requisito) |

## Consecuencias

- Los informes de adherencia comparan contra lo **último vigente** para ese operador y ese día (después de todos los ajustes), y aparte muestran cuántos ajustes hubo y por qué.
- Calidad puede auditar los desvíos respecto del esquema base por grupo.
- La UI necesita un editor de distribución (plano de la sala según la disposición física de los puestos, con la ola de cada uno) y una línea de tiempo de cobertura.
