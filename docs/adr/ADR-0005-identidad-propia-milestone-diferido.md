# ADR-0005 — Identidad propia del operador; integración con Milestone diferida

- **Estado:** Aceptado
- **Fecha:** 2026-11-18

## Contexto

No hay Active Directory. Las PCs están siempre encendidas y los operadores solo se loguean en Milestone XProtect Smart Client.
Sería ideal reutilizar ese login, pero faltan datos: versión de XProtect, si los usuarios son personales o compartidos, y si son Basic o Windows.
Según la documentación de Milestone:
- El Audit Log registra por defecto los inicios de sesión, pero no los cierres.
- Esos registros se leen con el MIP SDK (no por la REST API).
- Desde XProtect 2026 R1 hay audit logging con OpenTelemetry.

## Decisión

- **Ahora:** login propio en el agente (legajo + PIN), con el puesto autenticado por token de dispositivo.
- **Más adelante:**
  - Un puerto `IOperatorIdentityProvider` en Application, con una implementación `ManualLoginIdentityProvider`.
  - Una futura `MilestoneIdentityProvider` en Infrastructure correlacionará "login en XProtect desde la IP o host del puesto X" con el operador, y propondrá o abrirá la sesión.
  - Cruzar la actividad en Milestone (cambios de cámara, PTZ) servirá para detectar actividad simulada.

## Consecuencias

- Doble login para el operador mientras no exista la integración (Milestone + agente). Es aceptable en el piloto.
- El diseño permite enchufar la integración sin tocar el dominio.
- Pendiente: reunión con el coordinador técnico (pregunta Q11).
