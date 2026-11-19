# ADR-0010 — Autenticación de puestos y sesiones del agente con tokens opacos

- **Estado:** Propuesto
- **Fecha:** 2026-11-19

## Contexto

HU-04 exige que solo se conecten los puestos enrolados y que el Admin pueda revocarlos. HU-01 exige un "token de sesión corto"
para el operador (`03-arquitectura.md` §5). Además:
- Un relevo (RN-17) y una revocación tienen que cortar el acceso **en el momento**.
- Son unos 20 puestos en una LAN, así que validar contra la base en cada request no afecta el rendimiento (RNF-01).
- No hay Active Directory ni PKI de clientes.

## Decisión

1. **Código de enrolamiento:**
   - 10 caracteres alfanuméricos sin ambiguos (0/O, 1/I), generados con `RandomNumberGenerator`.
   - Uso único, vencimiento configurable (`EnrollmentCodeLifetime`).
   - Se guarda solo su hash SHA-256.
2. **Token de dispositivo:**
   - 256 bits aleatorios en Base64url.
   - Se entrega una sola vez al canjear el código y el servidor guarda solo su hash SHA-256.
   - No vence. Termina al revocar el puesto o al volver a enrolarlo.
3. **Token de sesión del operador:**
   - 256 bits aleatorios, guardado como hash en `WorkstationSession`.
   - Vence según `AgentSessionLifetime`.
   - Queda inválido en cuanto se cierra la sesión (logout, relevo, revocación).
4. **Transporte:**
   - Todas las llamadas del agente llevan `Authorization: Device <token>`.
   - Las que dependen de una sesión llevan además `X-Session-Token`.
   - Un esquema de autenticación propio de ASP.NET Core (`DeviceToken`) valida el dispositivo y, si corresponde, la sesión, y produce los claims `workstation_id` y `operator_id`.
   - La política `Agent` exige ese esquema.
5. **Hash:** SHA-256 sin sal alcanza, porque los tokens tienen 256 bits de entropía. La comparación es en tiempo constante (`CryptographicOperations.FixedTimeEquals`).
6. **En la PC:**
   - El token de dispositivo se guarda cifrado con DPAPI, alcance `LocalMachine`, porque hay una instancia por PC (ver la sección de Fase 1 en `05-preguntas-abiertas.md`).
   - El token de sesión vive solo en memoria.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| JWT firmado para la sesión | Para revocarlo en el momento (relevo, revocación) habría que consultar la base igual. Agrega un secreto de firma y su rotación. |
| Certificado de cliente (mTLS) por puesto | Requiere una PKI de clientes y su distribución. Es demasiado para el piloto; se puede revisar en la Fase 7. |
| API key fija en la configuración del agente | No distingue puestos y no se puede revocar uno solo. |

## Consecuencias

- Cada request del agente hace una consulta por índice sobre el hash. Con 20 puestos es despreciable.
- Revocar un puesto o cerrar una sesión tiene efecto inmediato.
- Si alguien copia el token de dispositivo cifrado con DPAPI `LocalMachine`, lo puede descifrar cualquier proceso de esa PC. Es aceptable porque las PCs son de uso exclusivo de la sala. Revocar el puesto mitiga el robo.
- Cuando llegue SignalR (Fase 4), el hub usa el mismo esquema `DeviceToken`.
