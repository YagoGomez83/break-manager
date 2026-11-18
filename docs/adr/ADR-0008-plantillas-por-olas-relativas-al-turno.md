# ADR-0008 — Plantillas de descanso por olas de puestos y horarios relativos al turno

- **Estado:** Aceptado — ampliado por ADR-0009 (esquemas flexibles por grupo)
- **Fecha:** 2026-11-18

## Contexto

El cronograma real de descansos (grupo C) asigna los horarios **a puestos**, no a personas. Los puestos se agrupan en columnas
(1-6-11-16-21, 2-7-12-17-22, …) que salen juntas. Hay 20 puestos físicos; las posiciones 21–25 son de relevo, así que el relevo ya está incluido en el esquema, y las columnas rotan para que haya una sola en descanso a la vez.
Los operadores tienen puesto fijo en el día, salvo los relevistas.

## Decisión

- La planificación tiene **dos niveles**:
  - **Cronograma de guardias** (quién trabaja cada día, importado de Excel).
  - **Plantilla de descansos** (olas de puestos con franjas).
- El **plan del turno** combina ambos: operador → puesto → ola → descansos concretos.
- Las franjas se guardan como **desplazamiento desde el inicio del turno** (`TimeSpan`) y duración. Así una plantilla puede servir a cualquier grupo con el mismo patrón, y se evitan errores con el turno que cruza la medianoche.
- La plantilla se valida al guardarla:
  - Franjas dentro del turno.
  - Sin solapes dentro de una ola.
  - Simultaneidad máxima entre olas (advertencia).
- El plan se valida al generarse, con la cobertura minuto a minuto.

## Alternativas consideradas

| Alternativa | Desventaja |
|---|---|
| Horarios por operador cargados a mano cada día | Mucho trabajo manual; no refleja cómo planifica hoy el negocio |
| Horarios absolutos (hora del día) por grupo | No reutilizable entre grupos; problemas con el turno 18–00 |
| Generación automática óptima (solver) | Sobrearquitectura: el patrón por olas ya resuelve el problema |

## Consecuencias

- Cambiar de puesto a un operador cambia automáticamente sus horarios de descanso (hereda la ola del puesto).
- Los relevistas ocupan las posiciones de relevo (21–25) y descansan con la ola de su posición (RN-32).
- En este ADR, "plantilla" equivale a lo que desde ADR-0009 se llama **esquema de descansos** (`BreakScheme`).
