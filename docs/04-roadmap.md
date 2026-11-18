# 04 — Roadmap por fases

Cada fase tiene una spec en `docs/fases/` que se entrega a Claude Code. Una fase se cierra con un PR revisado y sus criterios de salida cumplidos.

| Fase | Nombre | Contenido | Criterio de salida |
|---|---|---|---|
| 0 | Descubrimiento | Visión, requisitos, dominio, arquitectura, ADRs | Documentos aprobados; preguntas abiertas críticas respondidas |
| 1 | Esqueleto | Solución y capas, tests de arquitectura, Docker Compose dev, health checks, logging, CI | `dotnet test` en verde en CI; `docker compose up` levanta app + db |
| 2 | Identidad | Operadores, puestos, enrolamiento, login web por rol, login del agente, aviso de privacidad (HU-01..05) | Un agente enrolado se loguea y aparece en el servidor |
| 3 | Planificación | Importador del cronograma, esquema base y esquemas por grupo con validaciones, plan del turno con ajustes del día, relevistas, publicación (HU-10..15) | Plan del grupo C de noviembre generado desde los Excel reales |
| 4 | Presencia en el agente | Máquina de estados, Raw Input, avisos, descansos, outbox, heartbeat (HU-20..26) | Prueba piloto en 1 PC durante un turno completo sin pérdida de eventos |
| 5 | Supervisión en vivo | Tablero, clasificación y reclasificación, alertas de cobertura (HU-30..32) | Piloto con 1 grupo completo |
| 6 | Informes | Adherencia, ausencias, auditoría, exportación (HU-40..44) | Coordinación y Calidad validan los informes contra un turno conocido |
| 7 | Producción | Hardening, TLS interno, backups y restore probado, instalador del agente, runbook | Checklist de despliegue completo; restauración probada |
| 8 | Evolución | Integración con Milestone, retención automática, informe de cobertura (Post) | Según prioridad del negocio |

## Estrategia de implantación sugerida

1. **Piloto técnico:** 1 PC, 1 turno (fin de la Fase 4).
2. **Piloto operativo:** 1 grupo completo (por ejemplo el grupo C, del que ya tenemos el esquema), durante 2 semanas en paralelo con la planilla actual.
3. **Ajuste de umbrales** con datos reales: falsos positivos de inactividad, tolerancias.
4. **Despliegue en los grupos A, B y D.**
