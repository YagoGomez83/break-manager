# ADR-0007 — Idioma de documentación y código

- **Estado:** Aceptado
- **Fecha:** 2026-11-18

## Decisión

- **Documentación, ADRs, specs y textos de la interfaz:** español.
- **Código** (identificadores, namespaces, tablas, columnas, commits): inglés.
- La correspondencia entre términos de negocio e identificadores está en el **glosario** de `docs/02-dominio.md`. Todo término nuevo del dominio se agrega ahí antes de usarlo en el código.

## Motivo

Mantiene el lenguaje ubicuo (DDD) trazable para el negocio sin mezclar idiomas en el código.
Además, es compatible con librerías, ejemplos y herramientas, que están en inglés.
