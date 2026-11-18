# ADR-0002 — Stack tecnológico

- **Estado:** Aceptado
- **Fecha:** 2026-11-18

## Contexto

El núcleo del sistema es el **tiempo real** (presencia, avisos, tablero). El agente debe leer la entrada de mouse en Windows.
La web es para roles internos de la LAN. El equipo domina .NET, Python y PHP.

## Decisión

| Componente | Elección |
|---|---|
| Runtime | **.NET 10 (LTS, soporte hasta noviembre de 2028)**. .NET 11 es STS y termina su soporte en la misma fecha, sin el año de madurez que ya tiene .NET 10. |
| Servidor | ASP.NET Core 10: Minimal APIs + SignalR + Blazor Web App en modo *Interactive Server* |
| Persistencia | EF Core 10 + Npgsql sobre PostgreSQL |
| Agente | WPF (`net10.0-windows`), icono de bandeja (p. ej. H.NotifyIcon.Wpf), SQLite para el outbox |
| Importación de Excel | ClosedXML (MIT) |
| Autenticación web | ASP.NET Core Identity (cookies) con roles |
| Tests | xUnit, Testcontainers (PostgreSQL), NetArchTest o ArchUnitNET |
| Observabilidad | Serilog + OpenTelemetry |
| Sin | MediatR y AutoMapper (handlers explícitos y mapeo manual: menos dependencias y sin licencias comerciales) |

## Alternativas consideradas

| Alternativa | Ventajas | Desventajas |
|---|---|---|
| Agente .NET + backend Laravel/Filament | Filament acelera mucho el CRUD administrativo | Dos stacks; el tiempo real (Reverb) queda menos integrado con el agente; contratos duplicados |
| Electron/Tauri + FastAPI | Cliente multiplataforma | No hace falta (todo Windows); Electron es pesado; la entrada de mouse requiere módulos nativos |
| Web con React (SPA) | Más flexibilidad visual | Segundo stack; Blazor Server alcanza para 10 usuarios en la LAN |
| **Todo .NET** | Un lenguaje; contratos compartidos (`Contracts`); SignalR nativo en servidor, web y agente; P/Invoke directo | El CRUD administrativo requiere más trabajo que con Filament |

## Consecuencias

- Blazor Interactive Server mantiene un circuito por usuario web: con unos 10 usuarios no es un problema; no apto para internet público (no aplica).
- El agente solo corre en Windows (requisito confirmado).
