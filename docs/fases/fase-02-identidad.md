# Fase 2 — Identidad

> **Estado: BORRADOR — pendiente de aprobación.** Lo redactó Claude Code a partir de HU-01..05, ADR-0005, ADR-0006
> y `docs/03-arquitectura.md` §5 y §7. Todo lo que no sale de esas fuentes está marcado como **[Supuesto]**
> y figura en `docs/05-preguntas-abiertas.md` (Q17 en adelante).
>
> Spec para Claude Code. Leé antes `CLAUDE.md`, `docs/02-dominio.md`, ADR-0005, ADR-0006, ADR-0010 y ADR-0011.
> Presentá un plan antes de implementar.

## Objetivo

Que solo los puestos enrolados se conecten, que cada operador abra sesión en su puesto con legajo y PIN
después de aceptar el aviso de privacidad vigente, y que los usuarios web entren con usuario, contraseña y rol.

**Criterio de salida (roadmap):** un agente enrolado se loguea y aparece en el servidor.

## Alcance

### 1. Enrolamiento de puestos (HU-04)

- El Admin da de alta un puesto: número (1–20, único), fila, columna y hostname.
- El Admin genera un **código de enrolamiento** para el puesto:
  - Es de un solo uso y vence a las **24 h** **[Supuesto]**.
  - Se muestra una sola vez; en la base se guarda solo su hash.
  - Generar un código nuevo invalida el anterior sin usar.
- El agente canjea el código (`POST /api/agent/enrollment`) y recibe un **token de dispositivo** (ADR-0010):
  - El servidor guarda solo el hash del token.
  - El agente lo guarda con DPAPI.
  - Canjear de nuevo reemplaza el token anterior del puesto.
- Un agente sin token de dispositivo válido recibe `401` en todos los endpoints del agente, salvo el de enrolamiento.
- El Admin puede **revocar** un puesto:
  - Su token deja de valer de inmediato.
  - Si hay una sesión abierta, se cierra **[Supuesto: con un motivo nuevo `Revocation`; ver Q18]**.
  - Para volver a usar el puesto hace falta un enrolamiento nuevo.
- El puesto tiene `ServiceStatus` en el modelo, siempre *EnServicio* en esta fase. Marcarlo fuera de servicio (HU-06) se hace en la Fase 3.

### 2. Login del operador en el agente (HU-01)

- Pantalla de login: legajo + PIN. El PIN tiene **4 a 6 dígitos** **[Supuesto]**.
- `POST /api/agent/sessions`, autenticado con el token de dispositivo:
  - **Credenciales inválidas:** el mensaje es genérico ("Legajo o PIN incorrectos") y no revela si el legajo existe. Si el legajo existe, cuenta el intento fallido.
  - **5 intentos fallidos:** el legajo se bloquea por **5 minutos** y se registra el evento de seguridad `OperatorLockedOut`. Los dos valores son parámetros (`OperatorLockoutAttempts` y `OperatorLockoutDuration`), no constantes. Mientras dura el bloqueo, el mensaje lo informa sin decir el tiempo restante.
  - **Operador desactivado:** se responde igual que con credenciales inválidas.
  - **Aviso vigente sin aceptar:** responde `NoticeRequired`, con el texto del aviso y un **ticket de aceptación** (opaco, de un solo uso, 5 min). **No** se abre la sesión.
  - **Aviso al día:** se abre la sesión y se devuelve un **token de sesión** opaco (ADR-0010) que vence a las **8 h** **[Supuesto]**.
- Al abrir la sesión (RN-16, RN-17):
  - Si el puesto tenía una sesión abierta de **otro** operador, esa sesión se cierra con `EndReason = Relevo`.
  - Si el operador tenía una sesión abierta en **otro** puesto, esa sesión se cierra **[Supuesto: con `Relevo`; ver Q18]**.
  - El cierre y la apertura van en la misma transacción.
- `DELETE /api/agent/sessions/current` cierra la sesión con `EndReason = Logout`.
- En esta fase **no** hay estados de presencia. "Pasa a *Activo*" se cumple en la Fase 4; acá alcanza con que la sesión quede abierta.

### 3. Aviso de privacidad (HU-02)

- El Admin publica una **versión nueva** del aviso (texto + número de versión correlativo):
  - Las versiones anteriores se conservan y no se pueden editar.
  - La vigente es la última publicada.
- `POST /api/agent/sessions/notice-acceptance` recibe el ticket y la versión aceptada:
  - Registra la aceptación (operador, versión, `AcceptedAt` con la hora del servidor, puesto) y abre la sesión como en el punto 2, todo en una sola transacción.
  - Si la versión ya no es la vigente, responde `NoticeRequired` con la nueva.
- El agente muestra el aviso en una ventana sin botón para omitirlo. Si no se acepta, vuelve al login.
- Si **no hay ningún aviso publicado**, no se puede abrir ninguna sesión: se muestra "El sistema no está habilitado: falta publicar el aviso de privacidad" **[Supuesto; ver Q20]**.
- El texto del aviso **no** está en el código. En desarrollo se publica uno de prueba marcado "BORRADOR" (Q10).

### 4. Login web por rol (HU-03)

- ASP.NET Core Identity con cookie (ADR-0011):
  - La cookie es `Secure`, `HttpOnly` y `SameSite=Strict`.
  - Vence a las **8 h** con expiración deslizante **[Supuesto]**.
- Roles: `Administrator`, `Coordinator`, `Quality` y `Supervisor`. El supervisor tiene además un **grupo** (A–D), como claim.
- **Contraseña:** mínimo **12 caracteres**, sin otras reglas de composición **[Supuesto, alineado con ASVS nivel 1]**.
- **Bloqueo:** **5 intentos** fallidos bloquean la cuenta **15 minutos** **[Supuesto]**, y se registra un evento de seguridad.
- **Autorización:**
  - `FallbackPolicy` exige estar autenticado.
  - Hay una política por rol, y cada página y endpoint declara la suya.
  - Solo son anónimos, de forma explícita: `/health/*`, `/login`, `POST /api/agent/enrollment` y los endpoints del agente (usan su propio esquema, `DeviceToken`).
- **Páginas de esta fase:**
  - Login y logout.
  - Inicio (muestra el rol).
  - Las páginas de Admin del punto 5.
  - Supervisor, Coordinador y Calidad solo ven el inicio. Sus páginas llegan en las Fases 3 a 6.
- **Admin inicial:** se crea con el comando `migrate` desde las variables de entorno `BOOTSTRAP_ADMIN_USERNAME` y `BOOTSTRAP_ADMIN_PASSWORD`. Solo se crea si no existe ningún Admin. En el primer ingreso se exige cambiar la contraseña.
- **Rate limiting:** en `/login`, `POST /api/agent/enrollment` y `POST /api/agent/sessions*`, con límite por IP.

### 5. Gestión de operadores, usuarios y puestos (HU-05)

Páginas de Admin:

- **Operadores:**
  - Alta, modificación y desactivación: legajo (único), nombre, grupo y puesto habitual opcional.
  - Asignar o resetear el PIN. El PIN solo se carga; nunca se muestra.
  - **Nunca se borra** un operador.
- **Usuarios web:** alta, modificación de rol y grupo, desactivación y reseteo de contraseña. Un Admin no puede desactivarse a sí mismo.
- **Puestos:** alta, modificación, generación del código de enrolamiento y revocación.
- **Avisos de privacidad:** publicar una versión nueva y ver el historial.
- **Sesiones abiertas:** puesto, legajo del operador, inicio y hostname. Se actualiza al recargar la página; el tiempo real es de la Fase 5.

### 6. Eventos de seguridad

- Tabla `security_events`, **append-only**: sin `UPDATE` ni `DELETE`, igual que en ADR-0004.
- Columnas: `Id` (UUID v7), `Type`, `OccurredAt`, `ActorType` (Operator | User | Device | System), `ActorId?`, `WorkstationId?`, `Data` (JSON, sin datos personales).
- Tipos:
  - `OperatorLoginFailed`, `OperatorLockedOut`.
  - `UserLoginFailed`, `UserLockedOut`.
  - `EnrollmentCodeGenerated`, `WorkstationEnrolled`, `WorkstationRevoked`.
  - `InvalidDeviceToken`.
  - `PrivacyNoticePublished`.
- En los logs de Serilog van solo IDs, nunca legajos ni nombres.

### 7. Agente

- **Agent.Core** (sin WPF, con tests):
  - `AgentApiClient`.
  - Puerto `IDeviceCredentialStore`.
  - `LoginFlow` con los pasos *SinEnrolar → Enrolado → AvisoPendiente → SesiónAbierta*.
- **Agent.Windows:**
  - Ventanas de enrolamiento (URL del servidor + código), de login y del aviso.
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
  - Flujo completo del agente por HTTP: enrolar → login → `NoticeRequired` → aceptar → la sesión aparece en la lista de sesiones abiertas → login de otro operador en el mismo puesto → la sesión anterior queda cerrada con `Relevo`.
- **ArchitectureTests:** `Domain` y `Application` no referencian `Microsoft.AspNetCore.Identity`, y `Agent.Core` no referencia DPAPI.

### 9. Documentación

- `02-dominio.md`:
  - Agregar `EnrollmentCode`.
  - Indicar que `UserAccount` vive en Infrastructure (ADR-0011).
  - Agregar los parámetros nuevos a §10.
- `03-arquitectura.md` §5: los endpoints del agente.
- `desarrollo.md`: el admin inicial, cómo publicar un aviso de prueba y cómo enrolar un agente local.
- `deploy/.env.example`: las variables `BOOTSTRAP_ADMIN_*`.

## Criterios de aceptación

- [ ] `dotnet build` sin warnings y `dotnet test` en verde, incluido Testcontainers, y CI en verde.
- [ ] Un agente con un código válido se enrola. El mismo código, usado de nuevo o vencido, se rechaza.
- [ ] Un agente sin token, o con un token revocado, recibe `401`.
- [ ] Primer login de un operador: ve el aviso, lo acepta, la aceptación queda registrada con versión, hora del servidor y puesto, y la sesión aparece en "Sesiones abiertas".
- [ ] Al publicar una versión nueva del aviso, el siguiente login la pide de nuevo.
- [ ] Un segundo operador en el mismo puesto cierra la sesión anterior con `Relevo`. Un operador que entra en otro puesto cierra su sesión anterior.
- [ ] 5 PIN incorrectos bloquean el legajo 5 minutos y dejan `OperatorLockedOut` en `security_events`.
- [ ] Toda página y todo endpoint exige autenticación y política. El test de cobertura de autorización falla si se quita una política (documentarlo en el PR).
- [ ] Un usuario con rol Supervisor no puede entrar a las páginas de Admin (`403`).
- [ ] Ningún log contiene legajos, nombres, PIN ni tokens.

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
- Las respuestas a Q17–Q21, o los supuestos aplicados.
- El log de la verificación de los criterios de aceptación.
