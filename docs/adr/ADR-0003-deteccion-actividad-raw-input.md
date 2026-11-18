# ADR-0003 — Detección de actividad del mouse con Raw Input

- **Estado:** Aceptado
- **Fecha:** 2026-11-18

## Contexto

El negocio define la actividad como **uso del mouse** (zoom con la rueda, paneo y clics en Milestone). El teclado no cuenta.
`GetLastInputInfo` no distingue entre mouse y teclado. El agente convive con Milestone Smart Client y con antivirus corporativo.

## Decisión

Usar **Raw Input** (`RegisterRawInputDevices` con `RIDEV_INPUTSINK` sobre una ventana oculta) para recibir los eventos del mouse
(movimiento, botones y rueda) aunque el agente no tenga el foco.

- El agente **solo guarda el instante de la última entrada**. No guarda coordenadas, botones, teclas ni nada del contenido.
- La lógica de umbrales (2 y 3 min) vive en `Agent.Core` y recibe un "pulso de actividad" abstracto. Así se puede testear con un reloj falso.
- Se configura con una política `ActivityPolicy`: `MouseOnly` (por defecto) o `AnyInput` (usa además el teclado, o `GetLastInputInfo`).

## Alternativas consideradas

| Alternativa | Ventajas | Desventajas |
|---|---|---|
| `GetLastInputInfo` | Trivial y muy liviano | No distingue mouse de teclado |
| Hook global `WH_MOUSE_LL` | Simple | Algunos antivirus lo marcan; si el hilo se bloquea, el mouse del sistema se traba |
| Consultar `GetCursorPos` periódicamente | Trivial | No detecta la rueda (zoom) ni los clics sin movimiento |
| **Raw Input** | Detecta movimiento, clics y rueda; no inyecta código; bajo impacto | Un poco más de código Win32 |

## Consecuencias

- El agente debe correr en la **sesión interactiva** del usuario de Windows (no como servicio: la sesión 0 no recibe la entrada del usuario).
- Un "mouse jiggler" (hardware o software) puede simular actividad. Se mitiga más adelante cruzando con la actividad en Milestone (ADR-0005). Opcional en una fase posterior: descartar la entrada inyectada por software (flag `LLMHF_INJECTED`), lo que requeriría un hook y se evaluará aparte.
