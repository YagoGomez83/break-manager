# Desarrollo

Guía para compilar, probar y levantar BreakManager en una PC de desarrollo.

## Requisitos

| Herramienta | Versión | Para qué |
|---|---|---|
| .NET SDK | 10.0 (cualquier banda 10.0.1xx o superior; ver `global.json`) | Compilar y correr tests |
| Docker (Desktop o Engine) | Reciente, con Compose v2 | Tests de integración (Testcontainers) y entorno local |
| Windows 10/11 | — | Solo para compilar y ejecutar el agente WPF (`BreakManager.Agent.Windows`) |
| Git Bash o una shell POSIX | — | Ejecutar `scripts/dotnet-docker.sh` en Windows |

Las herramientas locales (`dotnet-ef`) están fijadas en `dotnet-tools.json`: `dotnet tool restore`.

## Estructura

```
BreakManager.sln            # solución completa
BreakManager.Server.slnf    # filtro sin el agente WPF (Linux / CI)
src/Server/                 # Domain, Application, Infrastructure, Web
src/Shared/                 # Contracts (servidor <-> agente)
src/Agent/                  # Agent.Core (testeable) y Agent.Windows (WPF)
tests/                      # unitarios, integración, arquitectura
deploy/                     # docker-compose.dev.yml y .env.example
scripts/dotnet-docker.sh    # dotnet dentro de un contenedor del SDK
```

Las versiones de los paquetes están centralizadas en `Directory.Packages.props`. `Directory.Build.props` activa
`Nullable`, `TreatWarningsAsErrors` y los analizadores `latest-recommended` para todos los proyectos.

## Compilar

```bash
dotnet build BreakManager.sln              # Windows: todos los proyectos
dotnet build BreakManager.Server.slnf      # Linux: servidor + Agent.Core + tests
```

Cualquier warning rompe el build.

## Tests

```bash
dotnet test --solution BreakManager.Server.slnf                 # todos
dotnet test --project tests/BreakManager.ArchitectureTests      # solo arquitectura
dotnet test --project tests/BreakManager.Infrastructure.Tests   # integración (requiere Docker)
dotnet test --project tests/BreakManager.Web.Tests              # host web + API del agente (requiere Docker)
```

- El runner es **Microsoft.Testing.Platform** (configurado en `global.json`). Por eso se usan `--solution`/`--project`.
- `BreakManager.Infrastructure.Tests` levanta `postgres:18` con Testcontainers: Docker tiene que estar corriendo.
- `BreakManager.ArchitectureTests` hace cumplir las reglas de dependencias de `CLAUDE.md`. Revisa tanto los `.csproj`
  (una referencia prohibida falla aunque no se use) como el IL compilado.

### Tests en contenedor

Algunas PCs no pueden ejecutar binarios recién compilados. Por ejemplo, Windows con **Smart App Control** activo
bloquea los DLL sin firma, y los tests fallan con "Una directiva de Control de aplicaciones bloqueó este archivo".
En ese caso se compila igual en Windows y los tests se corren en un contenedor Linux, el mismo entorno que CI:

```bash
scripts/dotnet-docker.sh dotnet test --solution BreakManager.Server.slnf
```

- El script monta el repo y el socket de Docker (para Testcontainers).
- Deja `bin/` y `obj/` en un volumen aparte (`breakmanager-artifacts`) para no mezclarlos con los de Windows.
- Guarda los paquetes NuGet en el volumen `breakmanager-nuget`.

**Red con TLS interceptado** (antivirus con escaneo HTTPS o proxy corporativo): si el restore falla con
`PartialChain`, hacé lo siguiente.

1. Exportá la CA raíz que usa el interceptor en formato PEM, por ejemplo a `%USERPROFILE%\.breakmanager\extra-ca.crt`.
2. Pasala con `EXTRA_CA_CERT`. Solo se agrega a la confianza del contenedor efímero.

```bash
EXTRA_CA_CERT=~/.breakmanager/extra-ca.crt scripts/dotnet-docker.sh dotnet test --solution BreakManager.Server.slnf
```

## Entorno local con Docker Compose

```bash
cp deploy/.env.example deploy/.env          # completar POSTGRES_PASSWORD (deploy/.env está en .gitignore)
docker compose -f deploy/docker-compose.dev.yml up -d --build
curl http://127.0.0.1:8080/health/live      # 200: el proceso responde
curl http://127.0.0.1:8080/health/ready     # 200: además, PostgreSQL responde
docker compose -f deploy/docker-compose.dev.yml down        # -v para borrar también la base
```

| Servicio | Qué hace |
|---|---|
| `db` | PostgreSQL 18 con un volumen nombrado y healthcheck. Publicado solo en `127.0.0.1:${DB_HOST_PORT}`. |
| `migrate` | Corre la imagen de la app con el argumento `migrate`, aplica las migraciones y la siembra de arranque, y termina. |
| `app` | La aplicación en `127.0.0.1:${APP_HOST_PORT}`. Arranca cuando `db` está sana y `migrate` terminó bien. |

- La imagen corre con un usuario **no root**: `docker compose -f deploy/docker-compose.dev.yml exec app whoami` → `app`.
- Si el build de la imagen falla por TLS interceptado, definí `EXTRA_CA_CERT` en `deploy/.env`. Se pasa como secreto
  de BuildKit solo a la etapa de build y no queda en la imagen final.

### Primer ingreso y datos de prueba

El comando `migrate` (servicio `migrate` del compose) siembra lo mínimo para empezar:

- Los roles web: `Administrator`, `Coordinator`, `Quality` y `Supervisor`.
- El **admin inicial**, solo si no existe ningún Administrador. Se crea con `BOOTSTRAP_ADMIN_USERNAME` y
  `BOOTSTRAP_ADMIN_PASSWORD` de `deploy/.env`. La contraseña tiene que tener 12 caracteres o más y no ser común.
- En `Development`, y solo si no hay ninguno publicado, un **aviso de privacidad de prueba** marcado "BORRADOR".
  El texto real lo redacta y publica la organización (Q10).

Pasos:

1. Completá `BOOTSTRAP_ADMIN_PASSWORD` en `deploy/.env` y levantá el compose.
2. Entrá a `http://127.0.0.1:8080/login` con el admin. La primera vez se pide cambiar la contraseña.
3. **Borrá `BOOTSTRAP_ADMIN_PASSWORD` de `deploy/.env`.** Ya no hace falta: el admin existe y la siembra no lo
   vuelve a crear.

Desde el menú de Admin: alta de puestos (y su código de enrolamiento), operadores (con PIN temporal), usuarios web,
aviso de privacidad y sesiones abiertas.

### Enrolar un agente local

El agente solo habla **HTTPS** y valida el certificado. El compose de desarrollo publica la app por HTTP (el TLS
llega con el reverse proxy en la Fase 7), así que para probar el agente se usa la web con `dotnet run` y el
certificado de desarrollo:

```bash
dotnet dev-certs https --trust
dotnet run --project src/Server/BreakManager.Web --launch-profile https    # https://localhost:7068
dotnet run --project src/Agent/BreakManager.Agent.Windows
```

1. En la web: **Puestos → Alta de puesto → Generar código**. El código se muestra una sola vez.
2. En el agente: servidor `https://localhost:7068` y el código. El token de dispositivo queda cifrado con DPAPI en
   `%ProgramData%\BreakManager\Agent\credential.bin`. Borrá ese archivo para "desenrolar" la PC.
3. Login con el legajo y el PIN temporal: aviso → cambio de PIN → "Sesión iniciada". La sesión aparece en
   **Sesiones abiertas**, y la página de Puestos muestra el último uso y la IP.

## Ejecutar la web sin Docker

```bash
dotnet user-secrets set "ConnectionStrings:BreakManager" \
  "Host=localhost;Port=5432;Database=breakmanager;Username=breakmanager;Password=<clave>" \
  --project src/Server/BreakManager.Web
dotnet run --project src/Server/BreakManager.Web
dotnet src/Server/BreakManager.Web/bin/Debug/net10.0/BreakManager.Web.dll migrate   # migraciones + siembra
```

Para la siembra del admin inicial, definí `BOOTSTRAP_ADMIN_USERNAME` y `BOOTSTRAP_ADMIN_PASSWORD` como variables
de entorno solo para ese comando. Los parámetros de identidad (bloqueos, vencimientos) están en la sección
`Identity` de la configuración; ver `docs/fases/fase-02-identidad.md`.

`appsettings.Development.json` trae una cadena **sin contraseña**. La contraseña va en user-secrets o en la variable
de entorno `ConnectionStrings__BreakManager`, nunca en el repositorio.

## Migraciones

- **Nunca se aplican solas al arrancar.**
- Crear una migración:

  ```bash
  dotnet tool restore
  dotnet ef migrations add <Nombre> \
    --project src/Server/BreakManager.Infrastructure \
    --startup-project src/Server/BreakManager.Web \
    --output-dir Persistence/Migrations
  ```

- Aplicarlas:
  - Con el binario publicado: `dotnet BreakManager.Web.dll migrate`. Usa la misma configuración que la app.
  - En desarrollo con Compose: lo hace el servicio `migrate` en cada `up`.
- Desde el contenedor, si la PC bloquea binarios locales:

  ```bash
  scripts/dotnet-docker.sh bash -c "dotnet tool restore && dotnet build src/Server/BreakManager.Web && \
    dotnet ef migrations add <Nombre> --project src/Server/BreakManager.Infrastructure \
    --startup-project src/Server/BreakManager.Web --output-dir Persistence/Migrations"
  ```

La tabla de historial se llama `__ef_migrations_history`, en snake_case como el resto de la base.

## Observabilidad

- **Logs:** JSON compacto de Serilog en la consola. `@tr` y `@sp` son el TraceId y el SpanId. Nivel configurable en
  la sección `Serilog` de `appsettings*.json`. Regla: solo IDs, nunca nombres de personas.
- **Métricas:** OpenTelemetry, de ASP.NET Core y del runtime. `Telemetry:Exporter` acepta `none` (por defecto),
  `console` (por defecto en Development) u `otlp`, que usa las variables estándar `OTEL_EXPORTER_OTLP_*`.
- **Health checks:** `/health/live` y `/health/ready`.

## Agente (Windows)

```bash
dotnet run --project src/Agent/BreakManager.Agent.Windows
```

- Arranca en la bandeja (menú "Abrir", "Cerrar sesión", "Acerca de" y "Salir") y muestra la ventana de enrolamiento
  o de login. Cerrar la ventana solo la oculta.
- Mantiene una sola instancia por PC con el mutex `Global\BreakManager.Agent`.
- Desde la Fase 2: enrolamiento, login con legajo y PIN, aviso de privacidad y cambio de PIN (ver "Enrolar un
  agente local"). Todavía no detecta actividad: Raw Input llega en la Fase 4.

## CI

`.github/workflows/ci.yml` corre en cada push a `main` y en cada PR:

1. **Servidor (Linux):** restore, build y test de `BreakManager.Server.slnf`, con Testcontainers.
2. **Windows:** build de la solución completa, tests de `Agent.Core` y `dotnet list package --vulnerable`. Falla con
   severidad alta o crítica.
3. **gitleaks:** escaneo de secretos.
4. **Imagen:** `docker build` y Trivy, que falla con vulnerabilidades críticas.
