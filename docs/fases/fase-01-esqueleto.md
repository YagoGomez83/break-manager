# Fase 1 — Esqueleto de la solución

> Spec para Claude Code. Leé antes `CLAUDE.md`, `docs/03-arquitectura.md` y los ADR 0001, 0002 y 0007.
> Presentá un plan antes de implementar.

## Objetivo

Dejar una solución .NET 10 compilable, testeable y desplegable en local, con las reglas de arquitectura
**ya automatizadas**, para que las fases siguientes solo agreguen funcionalidad.

## Alcance

### 1. Solución y proyectos

Crear `BreakManager.sln` con la estructura de `docs/03-arquitectura.md` §4:

| Proyecto | Tipo | Referencias permitidas |
|---|---|---|
| `BreakManager.Domain` | classlib | ninguna |
| `BreakManager.Application` | classlib | Domain |
| `BreakManager.Infrastructure` | classlib | Application, Domain |
| `BreakManager.Contracts` | classlib | ninguna |
| `BreakManager.Web` | ASP.NET Core (Blazor Web App, Interactive Server) | Application, Infrastructure, Contracts |
| `BreakManager.Agent.Core` | classlib | Contracts |
| `BreakManager.Agent.Windows` | WPF (`net10.0-windows`) | Agent.Core, Contracts |
| Proyectos de test (5) | xUnit | según lo que prueban |

- `Directory.Build.props` en la raíz con: `TargetFramework` net10.0 (salvo el agente WPF), `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors` true, `AnalysisLevel` latest-recommended.
- `Directory.Packages.props` con **Central Package Management**.
- `.editorconfig` con convenciones C# estándar (file-scoped namespaces, `var` donde el tipo es evidente).
- `global.json` fijando el SDK de .NET 10.
- `.gitignore` de .NET + `deploy/.env`.

### 2. Base del dominio (mínima, sin lógica de negocio todavía)

- En `Domain/Common`:
  - `Entity<TId>`, `AggregateRoot<TId>` (con lista de eventos de dominio).
  - Interfaz `IDomainEvent`.
  - `Result` / `Result<T>` con `Error` (código + mensaje).
- Carpetas vacías por módulo: `Planning/`, `Presence/`, `Identity/`, cada una con un `README.md` de una línea.

### 3. Application

- Interfaces genéricas de handler: `ICommandHandler<TCommand, TResult>` y `IQueryHandler<TQuery, TResult>`. Sin MediatR.
- Extensión `AddApplication(this IServiceCollection)` que registra los handlers por escaneo del ensamblado.
- FluentValidation registrado (sin validadores todavía).

### 4. Infrastructure

- `BreakManagerDbContext` (EF Core + Npgsql), con la convención **snake_case** (paquete `EFCore.NamingConventions`).
- Migración inicial vacía.
- Extensión `AddInfrastructure(IServiceCollection, IConfiguration)`.
- `TimeProvider.System` registrado como singleton.

### 5. Web (host)

- Health checks: `/health/live` (proceso) y `/health/ready` (incluye PostgreSQL).
- Serilog con salida JSON a la consola, enriquecido con `TraceId`.
- OpenTelemetry: métricas de ASP.NET Core y runtime, con exportador configurable (consola en desarrollo).
- Una página Blazor de inicio "BreakManager — en construcción" (sin autenticación todavía; la agrega la Fase 2).
- Cabeceras de seguridad básicas (`X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`); HSTS fuera de desarrollo.
- Las migraciones **no** se aplican automáticamente al arrancar en producción. Se ejecutan con un comando explícito y documentado.

### 6. Agente (cascarón)

- `Agent.Windows`: aplicación WPF que arranca minimizada en la bandeja, con menú "Acerca de" y "Salir", y una sola instancia (mutex con nombre).
- `Agent.Core`: vacío, salvo la interfaz `IActivitySource` (evento `ActivityDetected`). No implementar Raw Input todavía.

### 7. Tests

- `ArchitectureTests`, con tests que fallen si:
  - `Domain` referencia otro proyecto o algún paquete fuera de la BCL.
  - `Application` referencia `Infrastructure`, `Web` o EF Core.
  - `Agent.Core` referencia WPF, `System.Windows` o `Web`.
  - Hay tipos de `Infrastructure` públicos que no son extensiones de DI (opcional, como advertencia).
- `Domain.Tests`: tests de `Result` y `AggregateRoot` (registro y limpieza de eventos).
- `Infrastructure.Tests`: test de integración con **Testcontainers** que levanta PostgreSQL, aplica las migraciones y verifica que la conexión funciona.

### 8. Docker y despliegue local

- `src/Server/BreakManager.Web/Dockerfile` multi-stage: SDK para build y `aspnet` runtime para ejecución, con **usuario no root**.
- `deploy/docker-compose.dev.yml`:
  - Servicios `db` (PostgreSQL con versión mayor fijada, volumen nombrado, healthcheck) y `app` (depende de `db` sano).
  - Variables desde `deploy/.env` (crear `deploy/.env.example` sin secretos reales).
- La base de datos no se expone al host salvo en desarrollo, por un puerto configurable.

### 9. CI (GitHub Actions; la plataforma puede cambiar, ver Q8)

Workflow `ci.yml` en push y PR:
1. `dotnet restore` / `build` / `test` (Linux para el servidor; el agente WPF se compila en un job `windows-latest`).
2. `dotnet list package --vulnerable --include-transitive`: falla si hay severidad alta o crítica.
3. gitleaks (escaneo de secretos).
4. Build de la imagen Docker + escaneo con Trivy (falla en críticas).

### 10. Documentación

- Completar la sección **Comandos** de `CLAUDE.md` con los comandos reales.
- `docs/desarrollo.md`: requisitos locales, cómo levantar el entorno, cómo correr tests y migraciones.

## Criterios de aceptación

- [ ] `dotnet build BreakManager.sln` sin warnings, en Linux (proyectos del servidor) y en Windows (todos).
- [ ] `dotnet test` en verde, incluidos los tests de arquitectura y el de integración con Testcontainers.
- [ ] Romper deliberadamente una regla (p. ej. referenciar EF Core desde Application) hace fallar `ArchitectureTests`. Documentar la prueba en el PR.
- [ ] `docker compose -f deploy/docker-compose.dev.yml up -d` levanta `db` y `app`; `GET /health/ready` responde 200.
- [ ] La imagen corre con un usuario no root (`docker inspect` / `whoami` en el contenedor).
- [ ] El pipeline de CI pasa en el PR.
- [ ] No hay secretos en el repositorio (gitleaks limpio).

## Fuera de alcance

- Entidades de negocio, autenticación, SignalR hubs, Raw Input, importador de Excel. Corresponden a las Fases 2 a 4.
- Reverse proxy y TLS (Fase 7).

## Entregables

- PR con la solución, un resumen de decisiones menores tomadas, las preguntas surgidas y una captura o log de la ejecución de los criterios de aceptación.
