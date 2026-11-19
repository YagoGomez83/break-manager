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
| Q16 | ¿El operador puede cerrar el agente desde la bandeja ("Salir")? ¿O hace falta PIN o permiso de supervisor? | HU-26, RN-15 | Fase 1: "Salir" sin restricción (lo pide la spec); el cierre se ve como *Desconectado* | Fase 4 | 🟡 |

## Decisiones menores tomadas sin spec (Fase 1)

Elegidas por simplicidad; se pueden revisar en el PR.

| Tema | Decisión |
|---|---|
| Runner de tests | xUnit v3 sobre Microsoft.Testing.Platform (`global.json` → `"test": {"runner": "Microsoft.Testing.Platform"}`); sin `Microsoft.NET.Test.Sdk`. Aserciones con `Assert` de xUnit (FluentAssertions v8 tiene licencia comercial). |
| Tabla de historial de migraciones | `__ef_migrations_history` (snake_case, coherente con la convención de la base). |
| Instancia única del agente | Mutex `Global\BreakManager.Agent`: una instancia por PC aunque haya varias sesiones de Windows. |
| Migraciones | Comando explícito `BreakManager.Web.dll migrate`; en el compose de desarrollo, servicio one-shot `migrate`. |
| Métricas | Exportador `none` por defecto, `console` en Development, `otlp` opcional (`Telemetry:Exporter`). |
| Tests de arquitectura | Doble verificación: referencias en los `.csproj` (detecta un paquete aunque no se use) y dependencias reales en IL (NetArchTest). |
| Entorno de desarrollo con TLS interceptado | `EXTRA_CA_CERT` opcional para el script de contenedor y para el build de la imagen (secreto de BuildKit; no llega a la imagen final). |

## Decisiones menores tomadas sin spec (Fase 2)

Elegidas por simplicidad; se pueden revisar en el PR.

| Tema | Decisión |
|---|---|
| Re-login del mismo operador en el mismo puesto | Si el operador ya tiene la sesión abierta en ese puesto (p. ej. se reinició el agente y el token de sesión vivía solo en memoria), se **renueva el token** de esa sesión, sin cerrarla. No aplica `Relevo` ni `MovedToAnotherWorkstation` porque el operador no dejó el puesto. |
| Identificadores de `EndReason` | En código, en inglés: `Logout`, `Relief` (Relevo), `MovedToAnotherWorkstation`, `Revocation`, `Disconnection` (Desconexión), `EndOfShift` (FinDeTurno). |
| Hash del PIN | El puerto `IPinHasher` vive en `Domain`: el agregado `Operator` lo necesita para verificar que el PIN nuevo sea distinto del actual. Infrastructure lo implementa con PBKDF2 (ADR-0011). |
| Número de puesto | No se modifica después del alta: se editan fila, columna y hostname. Para renumerar se da de alta otro puesto. |
| PIN nuevo | Además de no ser trivial, tiene que ser distinto del PIN actual (el temporal). |
| Errores de concurrencia | Dos requests que guardan lo mismo a la vez (el mismo ticket, dos sesiones abiertas en un puesto) terminan en `ConcurrencyConflictException`. La API del agente responde `409` y el cliente puede reintentar. |
| Códigos HTTP de la API del agente | `400` error de negocio, `401` token de dispositivo o sesión inválidos, `423` legajo bloqueado, `503` sin aviso publicado, `409` conflicto. El cuerpo siempre es `AgentError(code, message)`. |
| Rate limiting | Ventana fija por IP en `/login` y en los endpoints de autenticación del agente: 20 pedidos por minuto, configurable en `RateLimiting:Auth`. Es un límite técnico, no un umbral de negocio. |
| Tablas de Identity | Con nombres snake_case (`users`, `roles`, `user_roles`, ...), igual que el resto de la base. |
| Acceso denegado | La página `/acceso-denegado` responde `403`; el middleware de cookies redirige ahí cuando el rol no alcanza. |
| Credencial del agente | Archivo `%ProgramData%\BreakManager\Agent\credential.bin`, cifrado con DPAPI (`LocalMachine`). DPAPI viene en el runtime de Windows Desktop: no se agregó el paquete `System.Security.Cryptography.ProtectedData`. |
| Cookie web en desarrollo | `Secure` siempre, salvo en `Development`, donde es `SameAsRequest`: el compose de desarrollo sirve por HTTP y una cookie `Secure` no vuelve por HTTP. |
| Agente y HTTPS en desarrollo | El compose de desarrollo publica la app por HTTP, pero el agente solo acepta HTTPS. Para probar el agente se usa `dotnet run --launch-profile https` con el certificado de desarrollo (`desarrollo.md`). |
| Lista de contraseñas comunes | `100k-most-used-passwords-NCSC.txt` de SecLists (MIT), en minúsculas y con gzip, como recurso embebido de Infrastructure. |
| Fallback de archivos de `MapStaticAssets` | No admite metadata de autorización. Lo cubre la `FallbackPolicy` (usuario autenticado) y hay un test que lo demuestra. |

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
| R15 (ex Q17) | Políticas de credenciales | PIN de 6 dígitos fijos, sin triviales (repetidos, secuencias, el legajo). El Admin asigna un PIN temporal y el operador lo cambia en el primer login (`MustChangePin`). Se aceptan los demás valores propuestos: contraseña web de 12+ caracteres con rechazo de contraseñas comunes (lista incluida, sin internet), bloqueo web 5 / 15 min, código de enrolamiento de 24 h, sesión del agente de 8 h, revalidación de la sesión web cada 5 min. Todo configurable. |
| R16 (ex Q18) | Motivo de cierre de sesión | `Revocation` al revocar el puesto. `MovedToAnotherWorkstation` cuando el mismo operador abre sesión en otro puesto. `Relevo` queda solo para cuando otro operador toma el puesto. |
| R17 (ex Q19) | ¿Sesión antes o después del aviso? | Después. Flujo: login → aviso → cambio de PIN → sesión. El ticket queda ligado al operador, al token de dispositivo y a la versión del aviso; se guarda como hash, es de un solo uso y se consume en la misma transacción que abre la sesión. |
| R18 (ex Q20) | Login sin aviso publicado | Se bloquea. El aviso "BORRADOR" se carga solo cuando `ASPNETCORE_ENVIRONMENT=Development`. |
| R19 (ex Q21) | Páginas web para Supervisor, Coordinador y Calidad en la Fase 2 | Solo el inicio. |
| R20 | Bloqueo del operador | Cada intento fallido registra el `WorkstationId`. 5 intentos → 5 min. Con 3 bloqueos en 24 h, el legajo queda bloqueado hasta que el Admin lo desbloquee desde la web (`OperatorUnlocked`). Todos los valores son parámetros. |
| R21 | Seguimiento de los tokens de dispositivo | Se registran `LastSeenAt` y la última IP de cada token. Un cambio de IP genera `DeviceTokenIpChanged`. La página de Puestos muestra los dos datos. |
