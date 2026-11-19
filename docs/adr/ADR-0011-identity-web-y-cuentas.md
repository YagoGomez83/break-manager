# ADR-0011 — ASP.NET Core Identity en Infrastructure y cuentas web fuera del dominio

- **Estado:** Aceptado
- **Fecha:** 2026-11-19

## Contexto

ADR-0002 elige ASP.NET Core Identity (cookies) para la web. Sin embargo:
- `02-dominio.md` lista `UserAccount` como agregado de Identity.
- La regla de dependencias prohíbe que `Domain` y `Application` referencien paquetes de ASP.NET.
- Identity ya resuelve hash de contraseñas, bloqueo, *security stamp* y cookies; reimplementarlo en el dominio sería más código y más riesgo.

## Decisión

1. **Las cuentas web viven en Infrastructure:**
   - `ApplicationUser : IdentityUser<Guid>` con `Group?` (solo para Supervisor) y `MustChangePassword`.
   - Roles de Identity: `Administrator`, `Coordinator`, `Quality` y `Supervisor`.
   - `UserAccount` **no** es una entidad de `Domain`.
2. **Puerto `IUserAccountService` en Application** (crear, cambiar rol o grupo, desactivar, resetear contraseña).
   - Los handlers de HU-05 dependen del puerto, no de Identity.
   - Las reglas propias, como "un Admin no se desactiva a sí mismo", viven en el handler.
3. **Las tablas de Identity van en el mismo `BreakManagerDbContext`**, con snake_case. No hay una segunda base.
4. **Revalidación de la sesión web:**
   - Cada `WebSessionRevalidationInterval` (5 min por defecto), con `SecurityStampValidator` para los requests y `RevalidatingServerAuthenticationStateProvider` para los circuitos de Blazor Server.
   - Desactivar un usuario, cambiarle el rol o resetearle la contraseña actualiza su *security stamp*: su sesión se corta en la siguiente revalidación.
5. **Contraseñas comunes:**
   - Un `IPasswordValidator` propio rechaza las contraseñas que aparecen en una lista incluida en el ensamblado de Infrastructure como recurso embebido. No usa internet.
   - La comparación no distingue mayúsculas de minúsculas.
   - La lista se toma de una fuente con licencia permisiva y su origen se documenta junto al archivo.
6. **El PIN del operador:**
   - Se hashea con `PasswordHasher<T>` de Identity (PBKDF2) detrás del puerto `ISecretHasher`. El hash queda en `Operator.PinHash`.
   - Las reglas del PIN (6 dígitos, no trivial, `MustChangePin`) son invariantes del dominio, no de Identity.
7. **Claves de DataProtection en PostgreSQL** (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`): las cookies sobreviven a reinicios y a reconstrucciones del contenedor.
8. **Admin inicial:**
   - Lo crea el comando `migrate` desde `BOOTSTRAP_ADMIN_USERNAME` y `BOOTSTRAP_ADMIN_PASSWORD`, solo si no hay ningún Admin.
   - Queda con `MustChangePassword = true`.
   - Después del primer ingreso, `BOOTSTRAP_ADMIN_PASSWORD` se borra del `.env` (`desarrollo.md`, `.env.example`).
9. **Los operadores no tienen cuenta web** (`03-arquitectura.md` §7).

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| `UserAccount` propio en `Domain`, con hash y bloqueo hechos a mano | Duplica Identity, con más superficie de error en seguridad. |
| `UserAccount` en `Domain` con Identity como "store" adaptado | Mucho código de adaptación para un CRUD administrativo con 10 usuarios. |
| Claves de DataProtection en un volumen de archivos | Un volumen más que respaldar. En la base quedan dentro del backup diario (RNF-09). |
| Consultar contraseñas filtradas en un servicio externo (p. ej. HIBP) | La VM no tiene acceso a internet y se enviaría información derivada de las contraseñas fuera de la LAN. |

## Consecuencias

- `02-dominio.md` §2 y §3 tienen que decir que `UserAccount` se implementa en Infrastructure.
- Los tests de arquitectura agregan la regla "`Domain` y `Application` no referencian `Microsoft.AspNetCore.Identity`".
- Dos paquetes nuevos en Infrastructure: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (MIT, de Microsoft).
- Desactivar un usuario tarda como máximo `WebSessionRevalidationInterval` en cortar su sesión, y cada revalidación hace una consulta por usuario conectado (unos 10).
