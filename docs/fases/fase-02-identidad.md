# Fase 2 — Identidad

> **Estado: Aprobada (2026-11-19).** Redactada a partir de HU-01..05, ADR-0005, ADR-0006, ADR-0010, ADR-0011
> y `docs/03-arquitectura.md` §5 y §7, con los cambios pedidos en la revisión. Las decisiones están en
> `docs/05-preguntas-abiertas.md` (R15–R19).
>
> Spec para Claude Code. Leé antes `CLAUDE.md`, `docs/02-dominio.md`, ADR-0005, ADR-0006, ADR-0010 y ADR-0011.
> Presentá un plan antes de implementar.

## Objetivo

Que solo los puestos enrolados se conecten, que cada operador abra sesión en su puesto con legajo y PIN
después de aceptar el aviso de privacidad vigente, y que los usuarios web entren con usuario, contraseña y rol.

**Criterio de salida (roadmap):** un agente enrolado se loguea y aparece en el servidor.

## Parámetros nuevos

Todos son configurables (sección `Identity` de la configuración). En esta fase no hay edición web (HU-50).

| Parámetro | Valor inicial | Uso |
|---|---|---|
| `OperatorLockoutAttempts` | 5 | Intentos fallidos de PIN antes de un bloqueo |
| `OperatorLockoutDuration` | 5 min | Duración de un bloqueo temporal |
| `OperatorLockoutsBeforeManualUnlock` | 3 | Bloqueos dentro de la ventana que dejan el legajo bloqueado hasta un desbloqueo manual |
| `OperatorLockoutWindow` | 24 h | Ventana en la que se cuentan esos bloqueos |
| `EnrollmentCodeLifetime` | 24 h | Vigencia del código de enrolamiento |
| `AgentSessionLifetime` | 8 h | Vigencia del token de sesión del agente |
| `LoginTicketLifetime` | 5 min | Vigencia del ticket de inicio de sesión (aviso y cambio de PIN) |
| `WebSessionLifetime` | 8 h (deslizante) | Vigencia de la cookie web |
| `WebSessionRevalidationInterval` | 5 min | Cada cuánto se revalida la sesión web contra la base |
| `WebLockoutAttempts` / `WebLockoutDuration` | 5 / 15 min | Bloqueo de usuarios web |
| `WebPasswordMinLength` | 12 | Largo mínimo de la contraseña web |

## Alcance

### 1. Enrolamiento de puestos (HU-04)

- El Admin da de alta un puesto: número (1–20, único), fila, columna y hostname.
- El Admin genera un **código de enrolamiento** para el puesto:
  - Es de un solo uso y vence según `EnrollmentCodeLifetime`.
  - Se muestra una sola vez; en la base se guarda solo su hash.
  - Generar un código nuevo invalida el anterior sin usar.
- El agente canjea el código (`POST /api/agent/enrollment`) y recibe un **token de dispositivo** (ADR-0010):
  - El servidor guarda solo el hash del token.
  - El agente lo guarda con DPAPI.
  - Canjear de nuevo reemplaza el token anterior del puesto.
- Un agente sin token de dispositivo válido recibe `401` en todos los endpoints del agente, salvo el de enrolamiento.
- **Último uso de cada token de dispositivo:**
  - Se registran `LastSeenAt` y `LastSeenIp`. La fecha se escribe como máximo una vez por minuto.
  - Si la IP cambia respecto de la anterior, se registra el evento de seguridad `DeviceTokenIpChanged`, con la IP anterior y la nueva.
  - Los dos datos se muestran en la página de Puestos.
- El Admin puede **revocar** un puesto:
  - Su token deja de valer de inmediato.
  - Si hay una sesión abierta, se cierra con `EndReason = Revocation`.
  - Para volver a usar el puesto hace falta un enrolamiento nuevo.
- El puesto tiene `ServiceStatus` en el modelo, siempre *EnServicio* en esta fase. Marcarlo fuera de servicio (HU-06) se hace en la Fase 3.

### 2. Login del operador en el agente (HU-01)

**PIN:**
- Tiene **6 dígitos** exactos.
- Se rechazan los PIN triviales:
  - todos los dígitos iguales (`111111`);
  - secuencias ascendentes o descendentes (`123456`, `654321`);
  - el legajo, o sus últimos 6 dígitos si el legajo es más largo.
- El Admin asigna un **PIN temporal**, que deja al operador con `MustChangePin = true`. En el primer login, el operador debe elegir un PIN nuevo, que cumpla las mismas reglas y sea distinto del temporal.

**Flujo** (`LoginFlow` en el agente): **login → aviso (si corresponde) → cambio de PIN (si corresponde) → sesión abierta**.

1. `POST /api/agent/sessions`, autenticado con el token de dispositivo, recibe legajo + PIN.
   - **Credenciales inválidas:** mensaje genérico ("Legajo o PIN incorrectos") que no revela si el legajo existe.
   - **Operador desactivado:** se responde igual que con credenciales inválidas.
   - **Sin pasos pendientes:** se abre la sesión y se devuelve el **token de sesión** (vence según `AgentSessionLifetime`).
   - **Con pasos pendientes:** responde `StepsRequired`, con la lista de pasos (`AcceptNotice` con el texto y la versión del aviso, `ChangePin`) y un **ticket de inicio de sesión**. **No** se abre la sesión.
2. `POST /api/agent/sessions/completion` recibe el ticket, la versión aceptada (si se pidió el aviso) y el PIN nuevo (si se pidió el cambio).
   - En **una sola transacción**: consume el ticket, registra la aceptación, cambia el PIN y abre la sesión.
   - Si la versión aceptada ya no es la vigente, responde `StepsRequired` otra vez con la versión nueva.

**Ticket de inicio de sesión:**
- Está ligado al **operador**, al **token de dispositivo** que hizo el login y a la **versión del aviso** pedida.
- Es opaco, se guarda solo como hash, es de un solo uso y vence según `LoginTicketLifetime`.
- Se rechaza si se presenta desde otro token de dispositivo, si ya se usó o si venció. Un ticket rechazado deja el evento `LoginTicketRejected`.

**Bloqueo:**
- Cada intento fallido de un legajo existente registra `OperatorLoginFailed` con el `WorkstationId`.
- Con `OperatorLockoutAttempts` intentos fallidos seguidos, el legajo se bloquea durante `OperatorLockoutDuration` y se registra `OperatorLockedOut`.
- **Bloqueo progresivo:** al llegar a `OperatorLockoutsBeforeManualUnlock` bloqueos dentro de `OperatorLockoutWindow`, el legajo queda **bloqueado hasta que el Admin lo desbloquee**. Se registra `OperatorLockedOut` con el indicador `RequiresManualUnlock`.
- Mientras dura el bloqueo, el agente informa "Legajo bloqueado" sin decir el tiempo restante.
- El **Admin desbloquea** el legajo desde la web, lo que pone en cero los intentos y los bloqueos de la ventana, y registra `OperatorUnlocked`.

**Apertura de la sesión** (RN-16, RN-17), en la misma transacción que la abre:
- Si el puesto tenía una sesión abierta de **otro** operador, se cierra con `EndReason = Relevo`.
- Si el mismo operador tenía una sesión abierta en **otro** puesto, se cierra con `EndReason = MovedToAnotherWorkstation`.

**Otros:**
- `DELETE /api/agent/sessions/current` cierra la sesión con `EndReason = Logout`.
- En esta fase **no** hay estados de presencia. "Pasa a *Activo*" se cumple en la Fase 4; acá alcanza con que la sesión quede abierta.

### 3. Aviso de privacidad (HU-02)

- El Admin publica una **versión nueva** del aviso (texto + número de versión correlativo):
  - Las versiones anteriores se conservan y no se pueden editar.
  - La vigente es la última publicada.
- La aceptación se registra con operador, versión, `AcceptedAt` (hora del servidor) y puesto, dentro de la transacción del paso 2 del flujo.
- El agente muestra el aviso en una ventana sin botón para omitirlo. Si no se acepta, vuelve al login.
- Si **no hay ningún aviso publicado**, no se puede abrir ninguna sesión: "El sistema no está habilitado: falta publicar el aviso de privacidad".
- El texto del aviso **no** está en el código. El comando `migrate` publica un aviso de prueba marcado "BORRADOR" **solo** si `ASPNETCORE_ENVIRONMENT=Development` y no hay ninguno publicado (Q10 sigue abierta para el texto real).

### 4. Login web por rol (HU-03)

- **ASP.NET Core Identity con cookie** (ADR-0011):
  - La cookie es `Secure`, `HttpOnly` y `SameSite=Strict`.
  - Vence según `WebSessionLifetime`.
- **Revalidación:**
  - Cada `WebSessionRevalidationInterval`, la sesión se revalida contra la base (*security stamp*). Vale tanto para los requests como para los circuitos de Blazor Server.
  - Desactivar un usuario, cambiarle el rol o resetearle la contraseña cambia su *security stamp*: la sesión abierta se corta en la siguiente revalidación.
- **Roles:** `Administrator`, `Coordinator`, `Quality` y `Supervisor`. El supervisor tiene además un **grupo** (A–D), como claim.
- **Contraseña:**
  - Mínimo `WebPasswordMinLength` caracteres, sin otras reglas de composición (ASVS nivel 1).
  - Se rechazan las **contraseñas comunes** de una lista incluida en el sistema, que funciona sin acceso a internet. La comparación no distingue mayúsculas de minúsculas.
- **Bloqueo:** `WebLockoutAttempts` intentos fallidos bloquean la cuenta durante `WebLockoutDuration`, y se registra un evento de seguridad.
- **Autorización:**
  - `FallbackPolicy` exige estar autenticado.
  - Hay una política por rol, y cada página y endpoint declara la suya.
  - Solo son anónimos, de forma explícita: `/health/*`, `/login`, `POST /api/agent/enrollment` y los endpoints del agente (usan su propio esquema, `DeviceToken`).
- **Páginas de esta fase:**
  - Login y logout.
  - Cambio de contraseña.
  - Inicio (muestra el rol).
  - Las páginas de Admin del punto 5.
  - Supervisor, Coordinador y Calidad solo ven el inicio. Sus páginas llegan en las Fases 3 a 6.
- **Admin inicial:**
  - Lo crea el comando `migrate` desde `BOOTSTRAP_ADMIN_USERNAME` y `BOOTSTRAP_ADMIN_PASSWORD`, solo si no existe ningún Admin.
  - En el primer ingreso se exige cambiar la contraseña.
  - Después del primer ingreso hay que **borrar `BOOTSTRAP_ADMIN_PASSWORD` del `.env`**.
- **Rate limiting:** en `/login`, `POST /api/agent/enrollment` y `POST /api/agent/sessions*`, con límite por IP.

### 5. Gestión de operadores, usuarios y puestos (HU-05)

Páginas de Admin:

- **Operadores:**
  - Alta, modificación y desactivación: legajo (único), nombre, grupo y puesto habitual opcional.
  - Asignar un PIN temporal. El PIN solo se carga; nunca se muestra.
  - Ver el estado de bloqueo y **desbloquear**.
  - **Nunca se borra** un operador.
- **Usuarios web:** alta, modificación de rol y grupo, desactivación y reseteo de contraseña. Un Admin no puede desactivarse a sí mismo.
- **Puestos:**
  - Alta, modificación, generación del código de enrolamiento y revocación.
  - Muestra `LastSeenAt` y `LastSeenIp`.
- **Avisos de privacidad:** publicar una versión nueva y ver el historial.
- **Sesiones abiertas:** puesto, legajo del operador, inicio y hostname. Se actualiza al recargar la página; el tiempo real es de la Fase 5.

### 6. Eventos de seguridad

- Tabla `security_events`, **append-only**: sin `UPDATE` ni `DELETE`, igual que en ADR-0004.
- Columnas: `Id` (UUID v7), `Type`, `OccurredAt`, `ActorType` (Operator | User | Device | System), `ActorId?`, `WorkstationId?`, `Data` (JSON, sin nombres ni secretos).
- Tipos:
  - Operador: `OperatorLoginFailed`, `OperatorLockedOut`, `OperatorUnlocked`, `OperatorPinReset`, `OperatorPinChanged`, `LoginTicketRejected`.
  - Usuarios web: `UserLoginFailed`, `UserLockedOut`.
  - Puestos: `EnrollmentCodeGenerated`, `WorkstationEnrolled`, `WorkstationRevoked`, `InvalidDeviceToken`, `DeviceTokenIpChanged`.
  - Aviso: `PrivacyNoticePublished`.
- En los logs de Serilog van solo IDs, nunca legajos, nombres, PIN ni tokens.

### 7. Agente

- **Agent.Core** (sin WPF, con tests):
  - `AgentApiClient`.
  - Puerto `IDeviceCredentialStore`.
  - `LoginFlow` con los pasos *SinEnrolar → Enrolado → Login → AvisoPendiente → CambioDePinPendiente → SesiónAbierta*.
  - El chequeo local de PIN trivial es solo para dar aviso al usuario; la validación real la hace el servidor.
- **Agent.Windows:**
  - Ventanas de enrolamiento (URL del servidor + código), de login, del aviso y de cambio de PIN.
  - `DpapiCredentialStore` con alcance `LocalMachine`.
  - Menú de bandeja: "Cerrar sesión".
  - Con la sesión abierta, muestra "Sesión iniciada: puesto N".
- El agente valida el certificado TLS del servidor; nunca se desactiva la validación.

### 8. Tests

- **Domain y Application:** TDD, con cobertura ≥ 80 % (RNF-12).
- **Infrastructure:** repositorios y migración contra PostgreSQL real (Testcontainers).
- **Nuevo proyecto `tests/BreakManager.Web.Tests`:**
  - Recorre todos los endpoints y páginas, y falla si alguno no tiene política ni `AllowAnonymous` explícito.
  - Tests de autorización por rol.
  - Flujos completos del agente por HTTP (ver los criterios de aceptación).
- **ArchitectureTests:** `Domain` y `Application` no referencian `Microsoft.AspNetCore.Identity`, y `Agent.Core` no referencia DPAPI.

### 9. Documentación

- `02-dominio.md`:
  - Agregar `EnrollmentCode` y el ticket de inicio de sesión.
  - Indicar que `UserAccount` vive en Infrastructure (ADR-0011).
  - Agregar los parámetros nuevos a §10.
  - Agregar `MustChangePin`, el estado de bloqueo y `LastSeenAt`/`LastSeenIp`.
- `03-arquitectura.md` §5: los endpoints del agente.
- `desarrollo.md`:
  - El admin inicial, y que hay que **borrar `BOOTSTRAP_ADMIN_PASSWORD` del `.env` después del primer ingreso**.
  - Cómo se publica el aviso de prueba en Development.
  - Cómo enrolar un agente local.
- `deploy/.env.example`: las variables `BOOTSTRAP_ADMIN_*`, con la misma advertencia.

## Criterios de aceptación

**General**
- [ ] `dotnet build` sin warnings, `dotnet test` en verde (incluido Testcontainers) y CI en verde.
- [ ] Ningún log contiene legajos, nombres, PIN ni tokens.

**Enrolamiento y puestos**
- [ ] Un agente con un código válido se enrola. El mismo código, usado de nuevo o vencido, se rechaza.
- [ ] Un agente sin token, o con un token revocado, recibe `401`.
- [ ] Revocar un puesto con sesión abierta la cierra con `EndReason = Revocation`.
- [ ] Cada request actualiza `LastSeenAt` y `LastSeenIp`. Un cambio de IP deja `DeviceTokenIpChanged`. La página de Puestos muestra los dos datos.

**PIN (punto 1)**
- [ ] No se acepta un PIN que no tenga exactamente 6 dígitos, ni uno trivial: repetido, en secuencia ascendente o descendente, o igual al legajo (o a sus últimos 6 dígitos).
- [ ] Un operador con PIN temporal pasa por el cambio de PIN antes de que se abra la sesión. El PIN nuevo no puede ser igual al temporal ni trivial.
- [ ] Primer login: login → aviso → cambio de PIN → sesión abierta. La sesión aparece en "Sesiones abiertas".

**Bloqueo (punto 2)**
- [ ] 5 PIN incorrectos bloquean el legajo 5 minutos y dejan `OperatorLockedOut`. Cada `OperatorLoginFailed` tiene el `WorkstationId`.
- [ ] El tercer bloqueo dentro de 24 h deja el legajo bloqueado hasta un desbloqueo manual: pasados los 5 minutos, sigue bloqueado.
- [ ] El Admin desbloquea el legajo desde la web, queda `OperatorUnlocked` y el operador puede volver a loguearse.
- [ ] Con otros valores en la configuración, el comportamiento cambia (los umbrales no son constantes).

**Cierre de sesiones (punto 3)**
- [ ] Otro operador en el mismo puesto cierra la sesión anterior con `Relevo`.
- [ ] El mismo operador en otro puesto deja su sesión anterior cerrada con `MovedToAnotherWorkstation`.

**Aviso y ticket (punto 4)**
- [ ] La aceptación queda registrada con versión, hora del servidor y puesto. Al publicar una versión nueva, el siguiente login la pide de nuevo.
- [ ] Un ticket presentado **desde otro token de dispositivo** se rechaza y deja `LoginTicketRejected`.
- [ ] Un ticket ya usado o vencido se rechaza. Si la apertura de la sesión falla, el ticket no queda consumido (misma transacción).
- [ ] Sin aviso publicado no se abre ninguna sesión. El aviso "BORRADOR" solo aparece en Development.

**Web**
- [ ] Toda página y todo endpoint exige autenticación y política. El test de cobertura de autorización falla si se quita una política (documentarlo en el PR).
- [ ] Un usuario con rol Supervisor no puede entrar a las páginas de Admin (`403`).
- [ ] Una contraseña de la lista de contraseñas comunes se rechaza.
- [ ] Al desactivar un usuario con sesión abierta, pierde el acceso en el siguiente `WebSessionRevalidationInterval` (con el intervalo reducido en el test).

## Fuera de alcance

- HU-06 (puestos fuera de servicio), que pasa a la Fase 3.
- Estados de presencia, Raw Input, heartbeat y SignalR (Fase 4). El tablero en vivo es de la Fase 5.
- Edición de parámetros desde la web (HU-50). En esta fase los parámetros se leen de la configuración.
- Recuperación de contraseña por email (no hay correo), autenticación de dos factores e integración con Milestone (ADR-0005).
- TLS con la CA interna y reverse proxy (Fase 7). En desarrollo se usa el certificado de desarrollo.

## Entregables

PR con:
- La implementación.
- Las decisiones menores tomadas.
- El log de la verificación de los criterios de aceptación.
