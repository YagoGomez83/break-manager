# ADR-0001 — Monolito modular con Clean Architecture

- **Estado:** Aceptado
- **Fecha:** 2026-11-18

## Contexto

20 puestos con agente, entre 15 y 22 operadores de monitoreo por turno (unos 130 en total entre los 4 grupos), 10 usuarios web, una VM on-premise y un equipo de mantenimiento chico.
Se necesita mantenibilidad y que el sistema pueda crecer (más grupos, integración con Milestone, nuevos informes).

## Decisión

Un **monolito modular**: un solo host servidor y una sola base de datos, organizado en capas de Clean Architecture
(Domain, Application, Infrastructure, Web) y en módulos de dominio (Identity, Planning, Presence, Reporting).
Los límites se hacen cumplir con **tests de arquitectura**.

## Alternativas consideradas

| Alternativa | Ventajas | Desventajas | Veredicto |
|---|---|---|---|
| Microservicios | Escalado y despliegue independientes | Complejidad operativa desproporcionada (red, observabilidad, consistencia) para 1 VM, 20 agentes y una decena de usuarios web | Descartada |
| Monolito en capas sin módulos | Lo más simple | Con el tiempo los módulos se acoplan | Descartada |
| **Monolito modular** | Simple de operar; límites claros; se puede extraer un módulo si alguna vez hace falta | Requiere disciplina, sostenida con tests de arquitectura | **Elegida** |

## Consecuencias

- Un solo `docker-compose` con app + db + proxy.
- Si un módulo necesitara escalar por separado (no se prevé), sus límites ya están definidos.
- CQRS solo "liviano": proyecciones de lectura en la misma base (sin event sourcing ni bases separadas).
