# 01 — Requisitos

Prioridad: **MVP** (versión 1 en producción) · **Post** (posterior). Las reglas RN-xx están en `02-dominio.md`.

## 1. Roles y permisos

| Capacidad | Operador (agente) | Supervisor | Coordinador | Calidad | Admin |
|---|:-:|:-:|:-:|:-:|:-:|
| Iniciar y finalizar sus descansos | ✔ | | | | |
| Ver el tablero en vivo | | ✔ | ✔ | | |
| Clasificar y reclasificar períodos | | ✔ (su grupo) | | | |
| Editar y publicar el plan del turno | | ✔ (su grupo) | ✔ | | |
| Ajustar el esquema de descansos del grupo | | ✔ (su grupo) | ✔ | | |
| Gestionar el esquema base | | | ✔ | | ✔ |
| Importar cronogramas | | | ✔ | | ✔ |
| Marcar puestos fuera de servicio | | ✔ | ✔ | | ✔ |
| Ver informes y exportar | | ✔ | ✔ | ✔ | |
| Ver la auditoría de reclasificaciones | | ✔ | ✔ | ✔ | |
| Usuarios, puestos, parámetros, catálogos y aviso de privacidad | | | | | ✔ |

## 2. Historias de usuario

### Épica E1 — Identidad y acceso

**HU-01 · Login del operador en el agente** — MVP
Como operador, quiero ingresar con mi legajo y PIN en el puesto para que mi actividad se registre a mi nombre.
- Dado un puesto enrolado, cuando ingreso un legajo y un PIN válidos, se abre una sesión en ese puesto y paso a *Activo*.
- Si el puesto ya tiene una sesión abierta de otro operador, esa sesión se cierra con motivo *Relevo* (RN-17).
- Tras 5 intentos fallidos se bloquea el legajo por 5 minutos y se registra un evento de seguridad.
- No puedo tener sesiones abiertas en dos puestos a la vez: abrir una nueva cierra la anterior.

**HU-02 · Aviso de privacidad** — MVP
Como organización, quiero que cada operador acepte el aviso vigente sobre el registro de actividad para cumplir el art. 71 de la LCT.
- En el primer login, o si hay una versión nueva, el agente muestra el aviso y no permite continuar sin aceptarlo.
- Se registran operador, versión, fecha y hora (servidor) y puesto.
- El texto lo publica un Admin. Las versiones anteriores se conservan.

**HU-03 · Login web por rol** — MVP
Como supervisor, coordinador o integrante de Calidad, quiero ingresar a la web con usuario y contraseña y ver solo lo que mi rol permite.
- Todas las páginas, endpoints y hubs exigen autenticación y una política de rol.
- Las contraseñas cumplen una política mínima; hay bloqueo tras intentos fallidos.

**HU-04 · Enrolamiento de puestos** — MVP
Como admin, quiero registrar cada PC como puesto (número, ubicación física en fila y columna, hostname) para que solo los agentes autorizados se conecten.
- El admin genera un código de enrolamiento de un solo uso. El agente lo canjea por un token de dispositivo.
- Un agente sin token válido no puede conectarse. El admin puede revocar un puesto.

**HU-06 · Marcar un puesto fuera de servicio** — MVP
Como supervisor, quiero marcar un puesto como fuera de servicio (por ejemplo, una PC rota) para que no se planifique y el sistema recalcule los relevistas.
- Pide un motivo; queda auditado con autor y fecha. Se puede volver a poner en servicio.
- Si pasa durante un turno publicado, el sistema propone reubicar al titular en un puesto vacante o dejarlo como relevista; decide el supervisor (RN-34).
- En el tablero, los puestos fuera de servicio se ven distinto y no cuentan para la cobertura.

**HU-05 · Gestión de operadores y usuarios** — MVP
Como admin, quiero dar de alta, modificar y desactivar operadores (legajo, grupo, puesto habitual, PIN) y usuarios web.

### Épica E2 — Planificación

**HU-10 · Importar el cronograma mensual de guardias** — MVP
Como coordinador, quiero importar el Excel mensual de guardias para no cargarlo a mano.
- Se reconocen las hojas por grupo (A–D) y se importa **solo la sección AO** (operadores de monitoreo). Se ignoran la sección AT 911 y la hoja de Analítica.
- Los operadores se emparejan por nombre exacto. Los nombres sin coincidencia se listan para darlos de alta o vincularlos.
- Se normalizan los códigos ("p" → "P"). Los códigos desconocidos generan un reporte de errores y **no** se importan en silencio.
- Las filas de totales (de Guardia, de Franco) no se importan.
- Antes de confirmar se muestra una vista previa con un resumen (operadores, días, conteo por código).
- Reimportar el mismo mes reemplaza el cronograma, salvo los días ya transcurridos.

**HU-11 · Gestionar el esquema base de descansos** — MVP
Como coordinador, quiero definir las olas, sus franjas (horario y duración) y la distribución de puestos por defecto.
- Puedo cargar las franjas como hora del día de un grupo de referencia; se guardan como desplazamiento desde el inicio del turno (RN-20).
- Al guardar se validan RN-21, RN-22 y RN-27 (bloqueantes) y RN-23 (advertencia), con los minutos en conflicto.
- Se muestra una línea de tiempo de cuántos puestos están en descanso en cada minuto.
- Cada cambio crea una versión nueva con vigencia (`ValidFrom`) y motivo (RN-29, RN-31).
- Datos iniciales: el esquema del grupo C (5 olas por columna, 6 descansos, 70 min), con la ola 4 a las 12:40.

**HU-12 · Generar el plan del turno** — MVP
Como supervisor, quiero generar el plan de mi turno a partir del cronograma y del esquema vigente de mi grupo.
- Toma los operadores AO con código G o C/G para esa fecha y grupo.
- Propone los puestos habilitados y las asignaciones operador → puesto (puesto habitual, si lo tiene).
- Habilita por defecto los puestos en servicio. Si hay más operadores que puestos habilitados, el excedente queda como relevista (p. ej. 17 personas y 15 puestos → 2 relevistas). El supervisor elige quiénes son (RN-25).
- Asigna a los relevistas las posiciones de relevo (21, 22, …) y calcula los descansos de todos según la ola de su posición (RN-32).
- Sugiere qué relevista cubre qué puesto en cada franja (RN-35).
- Advierte si la cobertura planificada queda por debajo del mínimo (RN-24).

**HU-13 · Ajustar y publicar el plan del turno** — MVP
Como supervisor, quiero ajustar el plan **solo para ese día** y publicarlo.
- Puedo: cambiar operadores de puesto; habilitar o deshabilitar puestos; cambiar la distribución del día (columna, fila, personalizada); mover una posición a otra ola; cambiar la posición de relevo de un relevista; cambiar qué relevista cubre qué puesto; quitar a quien no vino.
- Cada ajuste pide un motivo operativo y queda auditado (RN-29). No modifica el esquema del grupo (RN-28).
- Antes de publicar veo la línea de tiempo de cobertura resultante.
- Al publicar, los agentes reciben los descansos de su operador en menos de 5 s.

**HU-15 · Ajustar el esquema de descansos de mi grupo** — MVP
Como supervisor, quiero modificar el esquema de mi grupo por razones operativas para no tener que repetir el mismo ajuste todos los días.
- La primera modificación crea el esquema del grupo como copia del esquema base vigente (RN-30).
- Puedo cambiar la distribución (por columna, por fila, personalizada), mover posiciones (puestos o relevos) entre olas, y cambiar horarios y duraciones de franjas. Por ejemplo, "por fila" hace que los relevistas salgan todos en el último descanso de cada ronda.
- Se aplican las mismas validaciones que en HU-11. Se muestra la comparación con el esquema base y la cobertura resultante.
- Cada cambio crea una versión con vigencia y motivo obligatorio. Los planes ya publicados no cambian (RN-31).
- Puedo **restablecer** el esquema del grupo al base (también con motivo).
- Si Coordinación cambia el esquema base, veo un aviso y decido si incorporo el cambio.
- Solo puedo modificar el esquema de mi grupo (RN-33).

**HU-14 · Reprogramar un descanso en curso del día** — MVP
Como supervisor, quiero adelantar, posponer o cancelar un descanso con motivo, por ejemplo ante un evento crítico.
- El agente del operador refleja el cambio y vuelve a avisar en el nuevo horario.

### Épica E3 — Agente del operador

**HU-20 · Avisos de descanso planificado** — MVP
Como operador, quiero recibir un aviso antes y en el horario de mi descanso.
- Aviso previo (`PreNotice`) y aviso en el horario, visibles sobre las demás ventanas, sin robar el foco del Smart Client.
- El aviso indica el tipo y la duración del descanso.

**HU-21 · Iniciar y finalizar un descanso planificado** — MVP
- "Iniciar descanso" pasa al estado *DescansoPlanificado* y muestra una cuenta regresiva.
- "Volver" exige PIN (RN-13).
- Se aplican RN-11 (demora) y RN-12 (exceso).

**HU-22 · Declarar un descanso no planificado** — MVP
Como operador, quiero elegir de una lista por qué me ausento.
- Disponible desde *Activo* (botón "Salir") y desde el aviso de inactividad.
- El inicio se retrofecha según RN-03 cuando viene desde *Inactivo*.

**HU-23 · Detección de inactividad del mouse** — MVP
- Con la política `MouseOnly`, se considera entrada el movimiento, el clic y la rueda (ADR-0003).
- Se aplican RN-01, RN-02 y RN-06.
- Al pasar a *Inactivo*, el agente muestra un aviso con el selector de tipo. A los 3 min pasa a *Ausente*.
- El agente **no** registra teclas, coordenadas, capturas ni nombres de procesos.

**HU-24 · Heartbeat y reconexión** — MVP
- El agente envía un heartbeat cada `HeartbeatInterval`. El servidor aplica RN-15.
- Al reconectar, el estado se sincroniza sin duplicar eventos (idempotencia por ID de evento).

**HU-25 · Operación sin conexión** — MVP
- Sin conexión, el agente guarda los eventos en una cola local (SQLite) y los envía al reconectar, en orden.
- Los eventos diferidos llevan la hora corregida por el desfase de reloj medido (ADR-0004) y quedan marcados como *diferidos*.

**HU-26 · Arranque automático y protección básica** — MVP
- El agente se inicia con la sesión de Windows, con una sola instancia por PC.
- Si el agente se cierra, el puesto pasa a *Desconectado* en el tablero (RN-15).

### Épica E4 — Supervisión en vivo

**HU-30 · Tablero de puestos y operadores** — MVP
Como supervisor, quiero ver en vivo cada puesto (cubierto o descubierto, quién está) y cada operador (estado, desde cuándo).
- Las actualizaciones llegan por SignalR en menos de 2 s.
- Colores por estado; los excesos (RN-12), las demoras (RN-11) y los ausentes se destacan.
- Se muestran los relevistas disponibles, la próxima ola de descanso y el contador de puestos cubiertos frente al mínimo.

**HU-31 · Clasificar una inactividad o una ausencia** — MVP
- Desde el tablero, el supervisor elige el tipo para un operador *Inactivo* o *Ausente* (RN-05, RN-07).
- La reclasificación pide un motivo y queda auditada.

**HU-32 · Alerta de cobertura mínima** — MVP
- Alerta visual y sonora (configurable) cuando los puestos habilitados cubiertos son menos que el mínimo del grupo (RN-19).

**HU-33 · Gestionar relevos en vivo** — MVP
Como supervisor, quiero cambiar en el momento qué relevista cubre qué puesto, o quién sale a descanso, según la operativa.
- Desde el tablero puedo reasignar un relevista o reprogramar descansos (RN-14), con motivo.

### Épica E5 — Informes (Coordinación y Calidad)

**HU-40 · Informe de adherencia** — MVP
Por operador, turno, grupo y rango de fechas: descansos planificados frente a tomados, demoras, excesos, no tomados y minutos totales.

**HU-41 · Informe de ausencias e inactividad** — MVP
Episodios de inactividad (> 2 min) y ausencias, con duración, clasificación final, reclasificaciones y quién las hizo.

**HU-42 · Informe de cobertura** — Post
Minutos con cobertura por debajo del mínimo, por turno.

**HU-43 · Exportación** — MVP
Todos los informes se exportan a Excel (.xlsx) con los filtros aplicados.

**HU-44 · Auditoría** — MVP
Listado de reclasificaciones, reprogramaciones, ajustes del día y cambios de esquema (base y de grupo): quién, cuándo, valor anterior, valor nuevo y motivo.

### Épica E6 — Administración

**HU-50 · Parámetros** — MVP: edición de los parámetros de `02-dominio.md` §10, con auditoría.
**HU-51 · Catálogo de descansos no planificados** — MVP: alta, baja y modificación (desactivar, nunca borrar si tiene uso).
**HU-52 · Retención de datos** — Post: purga automática de eventos con más antigüedad que `DataRetention`.
**HU-53 · Integración con Milestone** — Post: identificación automática del operador por el login de XProtect (ADR-0005).

## 3. Requisitos no funcionales

| ID | Categoría | Requisito |
|---|---|---|
| RNF-01 | Rendimiento | 20 agentes (uno por puesto, con holgura para 50) y 10 usuarios web concurrentes en la VM, con p95 de API < 300 ms. |
| RNF-02 | Tiempo real | Evento del agente → tablero en < 2 s en la LAN. |
| RNF-03 | Disponibilidad | Operación 24×7. Ante caída del servidor, los agentes siguen funcionando con cola offline y sin pérdida de eventos. |
| RNF-04 | Integridad | Eventos de presencia append-only. Las correcciones son eventos nuevos y auditados. |
| RNF-05 | Tiempo | Hora oficial del servidor (sincronizado por NTP). El desfase del reloj del agente se mide y compensa. |
| RNF-06 | Seguridad | TLS en todo tráfico. Autenticación de usuarios y de dispositivos. Autorización por políticas. OWASP ASVS nivel 1 como base. |
| RNF-07 | Privacidad | Minimización: solo eventos de estado. Aviso versionado aceptado. Retención definida. |
| RNF-08 | Observabilidad | Logs estructurados, métricas (agentes conectados, eventos por minuto, latencia) y health checks. |
| RNF-09 | Recuperación | Backup diario de PostgreSQL con prueba de restauración mensual. RPO ≤ 24 h, RTO ≤ 4 h. |
| RNF-10 | Compatibilidad | Agente en Windows 10/11 x64. Web en Chrome y Edge actuales. |
| RNF-11 | Usabilidad | Iniciar o volver de un descanso en ≤ 2 acciones (+ PIN). El agente no interfiere con Milestone Smart Client. |
| RNF-12 | Mantenibilidad | Cobertura de tests ≥ 80 % en Domain y Application. Tests de arquitectura en CI. |
| RNF-13 | Despliegue | Docker Compose en la VM. Agente distribuido como instalador firmado (si hay certificado) o paquete versionado. |
