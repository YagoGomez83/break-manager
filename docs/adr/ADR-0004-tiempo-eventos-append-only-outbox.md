# ADR-0004 — Tiempo oficial, eventos append-only y outbox en el agente

- **Estado:** Aceptado
- **Fecha:** 2026-11-18

## Contexto

Los registros de presencia pueden usarse para sanciones o reclamos: tienen que ser **confiables y no repudiables**.
El operador podría cambiar la hora de la PC, y la red o el servidor pueden caerse.

## Decisión

1. **Hora oficial del servidor**, sincronizado por NTP.
2. **Medición del desfase:**
   - Cada heartbeat devuelve la hora del servidor; el agente estima su desfase (estilo NTP: ida y vuelta / 2).
   - El agente usa un **reloj monotónico** (`Stopwatch`) para medir intervalos, así un cambio de la hora de Windows no altera las duraciones.
3. **Cada evento lleva dos tiempos:**
   - `OccurredAt`: hora estimada en la que ocurrió (reloj del agente corregido por el desfase).
   - `ReceivedAt`: hora en que llegó al servidor.
   - En línea, el servidor usa `ReceivedAt` como oficial si la diferencia es menor a una tolerancia (2 s). Si el evento es diferido (offline), usa `OccurredAt` y lo marca como `Deferred`.
   - Si el desfase estimado supera un límite (p. ej. 60 s), el evento se marca `ClockSuspect` para auditoría.
4. **Eventos append-only:**
   - `PresenceEvent` nunca se actualiza ni se borra (hay restricción en la base y un permiso de usuario de BD sin `UPDATE`/`DELETE` sobre esa tabla).
   - Las correcciones (reclasificaciones, reprogramaciones) son eventos nuevos con autor y motivo.
5. **Outbox en el agente:**
   - Los eventos se escriben primero en SQLite local y luego se envían por lotes.
   - Cada evento tiene un `EventId` UUID v7, ordenable por tiempo, y el servidor descarta duplicados (**idempotencia**).

## Alternativas consideradas

- **Confiar en la hora de la PC:** simple, pero manipulable. Descartada.
- **Event sourcing completo:** la trazabilidad es máxima, pero la complejidad es alta. Se adopta solo la parte útil: un log append-only de presencia más un estado actual proyectado.

## Consecuencias

- El estado actual (`OperatorPresence`) es una proyección que se puede reconstruir desde los eventos.
- Los informes pueden mostrar con transparencia qué registros fueron diferidos o tienen el reloj en duda.
