# BreakManager — Sistema de gestión de descansos y presencia

Sistema para planificar, notificar y registrar los descansos de los operadores de un centro de monitoreo,
con detección de inactividad en el puesto y tablero en vivo para supervisión.

> Nombre de código provisorio: **BreakManager**. Puede cambiarse antes de la Fase 1.

## Cómo está organizado este repositorio

| Ruta | Contenido |
|---|---|
| `CLAUDE.md` | Instrucciones para Claude Code (rol, reglas de arquitectura, convenciones, DoD) |
| `docs/00-vision.md` | Problema, objetivos, alcance y métricas de éxito |
| `docs/01-requisitos.md` | Roles, historias de usuario con criterios de aceptación y requisitos no funcionales |
| `docs/02-dominio.md` | Lenguaje ubicuo, modelo de dominio, máquina de estados y reglas de negocio |
| `docs/03-arquitectura.md` | Arquitectura, estructura de la solución, despliegue, seguridad y observabilidad |
| `docs/04-roadmap.md` | Fases del proyecto y criterios de salida |
| `docs/05-preguntas-abiertas.md` | Decisiones pendientes con el negocio |
| `docs/adr/` | Architecture Decision Records |
| `docs/fases/` | Especificaciones ejecutables por fase (lo que se le entrega a Claude Code) |

## Forma de trabajo

- **Ingeniería (humanos + asistente de arquitectura):** definen requisitos, dominio, decisiones (ADR) y la spec de cada fase.
- **Claude Code (desarrollador):** implementa una fase por vez a partir de `docs/fases/fase-NN-*.md`, respetando `CLAUDE.md`.
- Cada fase termina con revisión humana (PR) antes de pasar a la siguiente.

## Estado

Fase 0 — Descubrimiento: **completa a nivel borrador**, con preguntas abiertas en `docs/05-preguntas-abiertas.md`.
Próxima: Fase 1 — Esqueleto de la solución (`docs/fases/fase-01-esqueleto.md`).
