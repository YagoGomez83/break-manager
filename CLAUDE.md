# CLAUDE.md — Guía para Claude Code

## Contexto en 30 segundos

BreakManager gestiona descansos y presencia de los operadores de monitoreo de un centro de monitoreo
(20 puestos, 15 a 22 operadores por turno, 4 turnos de 6 h, grupos A–D). Los operadores de 911 quedan fuera del alcance.
Tiene tres piezas:

1. **Agente de escritorio (Windows, WPF):** avisa descansos, permite iniciarlos/finalizarlos, detecta inactividad del mouse.
2. **Servidor (ASP.NET Core en una VM Linux on-premise, solo LAN):** API + SignalR + web Blazor para supervisión, coordinación y calidad.
3. **PostgreSQL:** persistencia.

Antes de escribir código leé, en este orden: `docs/02-dominio.md`, `docs/03-arquitectura.md`, los ADR en `docs/adr/`
y la spec de la fase que te asignaron en `docs/fases/`.

## Tu rol

- Sos el **desarrollador**. Los requisitos y las decisiones de arquitectura los toman los ingenieros del proyecto.
- **No inventes requisitos.** Si algo no está especificado, preguntá. Si no podés preguntar, elegí la opción más simple,
  dejala anotada en `docs/05-preguntas-abiertas.md` y mencionala en el resumen del PR.
- Si necesitás tomar una decisión de diseño que no cubre ningún ADR, proponé un ADR nuevo con estado **Propuesto**.
- Trabajá **solo dentro del alcance de la fase**. Lo que quede fuera, anotalo como sugerencia; no lo implementes.

## Flujo de trabajo

1. Leé la spec de la fase y presentá un **plan** (plan mode) antes de tocar código.
2. Implementá en pasos chicos. En `Domain` y `Application`, **tests primero** (TDD).
3. Commits pequeños con **Conventional Commits** (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`).
4. Al terminar: `dotnet build` sin warnings, `dotnet test` en verde y los docs actualizados si cambió el comportamiento.

## Reglas de arquitectura (no negociables)

- **Regla de dependencias (Clean Architecture):** `Domain` ← `Application` ← `Infrastructure` / `Web` / `Agent.Windows`.
  - `Domain` no referencia ningún otro proyecto ni paquetes de infraestructura (nada de EF Core, ASP.NET, SignalR, ClosedXML).
  - `Application` define **puertos** (interfaces); `Infrastructure` los implementa.
  - `Web` y `Agent.Windows` son *composition roots*: solo cablean dependencias y adaptan entrada/salida.
- La lógica de presencia del agente vive en `Agent.Core` (sin WPF, sin Win32) para poder testearla. Win32 y Raw Input viven en `Agent.Windows`.
- Los **tests de arquitectura** (`tests/BreakManager.ArchitectureTests`) hacen cumplir estas reglas. No los desactives para "hacer pasar" algo.
- Los módulos del dominio (Planning, Presence, Identity, Reporting) se comunican por interfaces de `Application` o por eventos de dominio, nunca accediendo a las entidades internas del otro.
- Sin MediatR ni AutoMapper: handlers explícitos registrados en DI y mapeo manual. Menos magia, menos dependencias.

## Convenciones de código

- .NET 10 (LTS), C# con `Nullable` habilitado, `TreatWarningsAsErrors=true`, namespaces *file-scoped*.
- **Identificadores en inglés** según el glosario de `docs/02-dominio.md` (columna "Identificador"). Textos para el usuario en **español**.
- **Tiempo:**
  - Nunca `DateTime.Now` ni `DateTime.UtcNow`: inyectá `TimeProvider`.
  - Instantes en UTC (`DateTimeOffset`, columna `timestamptz`).
  - Horarios de los esquemas de descanso como desplazamientos (`TimeSpan`) desde el inicio del turno.
  - Zona operativa: `America/Argentina/Buenos_Aires`.
- Errores de negocio esperables → `Result`/`Result<T>`. Excepciones solo para lo excepcional.
- Validación de entrada en el borde (Application, FluentValidation). Invariantes, dentro de las entidades del dominio.
- `async` de punta a punta, con `CancellationToken` en toda operación de E/S.
- Registros de presencia **append-only**: nunca `UPDATE`/`DELETE` sobre eventos. Las correcciones son eventos nuevos (ver ADR-0004).

## Seguridad (Security by Design)

- Nunca commitees secretos. Configuración sensible por variables de entorno, `dotnet user-secrets` en desarrollo y `.env` fuera del repo.
- Autorización por **políticas** basadas en rol en cada endpoint, hub y página. Nada queda abierto por defecto.
- Sin SQL concatenado: EF Core o consultas parametrizadas.
- **Datos personales mínimos:**
  - En los logs, solo IDs (nunca nombres).
  - El agente **no** captura teclas, posición del mouse, pantallas ni procesos (ver ADR-0003 y ADR-0006).
- HTTPS siempre, incluso en la LAN.
- Dependencias: no agregues paquetes sin justificarlo en el PR; preferí licencias permisivas (MIT, Apache-2.0).

## Comandos

> Se completan en la Fase 1.

```bash
dotnet build BreakManager.sln
dotnet test BreakManager.sln
docker compose -f deploy/docker-compose.dev.yml up -d
```

## Definition of Done (por historia o tarea)

- [ ] Criterios de aceptación de la historia cubiertos por tests automatizados.
- [ ] Build sin warnings y tests en verde (unitarios, de integración y de arquitectura).
- [ ] Sin secretos ni datos personales en código, logs o fixtures.
- [ ] Documentación actualizada (dominio, arquitectura o ADR) si cambió el comportamiento o una decisión.
- [ ] Resumen del PR con: qué se hizo, decisiones tomadas, preguntas abiertas y cómo probarlo.

## Qué NO hacer

- No agregar microservicios, colas de mensajería, Kubernetes ni CQRS con bases separadas: es un monolito modular (ADR-0001).
- No implementar la integración con Milestone: está diferida (ADR-0005).
- No cambiar umbrales de negocio "a mano" en código: son parámetros configurables (ver `docs/02-dominio.md`, sección Parámetros).
