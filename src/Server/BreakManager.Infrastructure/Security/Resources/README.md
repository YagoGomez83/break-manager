# Lista de contraseñas comunes

`common-passwords.txt.gz` es la lista que usa `CommonPasswordValidator` para rechazar contraseñas web comunes, sin acceso a internet (ADR-0011).

- **Origen:** `Passwords/Common-Credentials/100k-most-used-passwords-NCSC.txt` del repositorio [SecLists](https://github.com/danielmiessler/SecLists), rama `master`, descargado el 2026-11-19.
- **Licencia:** MIT, de Daniel Miessler. El aviso completo está en `common-passwords.LICENSE.txt`.
- **Transformación:** se pasó a minúsculas, se quitaron duplicados y líneas vacías, y se comprimió con gzip. Quedaron 97 746 entradas.

Para actualizarla, repetí esa transformación con una versión nueva del archivo y actualizá la fecha de este README.
