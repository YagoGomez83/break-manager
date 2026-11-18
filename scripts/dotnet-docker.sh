#!/usr/bin/env bash
# Ejecuta un comando en un contenedor del SDK de .NET 10 con el repositorio montado.
#
# Útil cuando la PC no puede ejecutar binarios compilados localmente (p. ej. Windows con
# Smart App Control) y para reproducir el entorno Linux de CI. Monta el socket de Docker para
# que los tests con Testcontainers puedan levantar PostgreSQL.
#
# Si la red intercepta TLS (antivirus con escaneo HTTPS o proxy corporativo), exportá la CA raíz
# en formato PEM y pasala con EXTRA_CA_CERT=/ruta/a/ca.crt: se agrega a los certificados de
# confianza de ese contenedor efímero, nada más.
#
# Ejemplos:
#   scripts/dotnet-docker.sh dotnet test --solution BreakManager.Server.slnf
#   scripts/dotnet-docker.sh bash -c "dotnet tool restore && dotnet ef migrations list ..."
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if command -v cygpath >/dev/null 2>&1; then
  repo="$(cygpath -w "$repo")"   # Git Bash en Windows: Docker necesita la ruta de Windows.
fi

ca_args=()
if [ -n "${EXTRA_CA_CERT:-}" ]; then
  ca_path="$EXTRA_CA_CERT"
  if command -v cygpath >/dev/null 2>&1; then ca_path="$(cygpath -w "$ca_path")"; fi
  ca_args=(-v "$ca_path:/usr/local/share/ca-certificates/extra-ca.crt:ro")
fi

tty_flag=()
if [ -t 1 ]; then tty_flag=(-t); fi

MSYS_NO_PATHCONV=1 exec docker run --rm "${tty_flag[@]}" \
  -v "$repo:/src" -w /src \
  -v breakmanager-nuget:/root/.nuget/packages \
  -v breakmanager-artifacts:/artifacts \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e BREAKMANAGER_ARTIFACTS_PATH=/artifacts \
  -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e DOTNET_NOLOGO=1 \
  --add-host host.docker.internal:host-gateway \
  "${ca_args[@]}" \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -c 'update-ca-certificates >/dev/null 2>&1; exec "$@"' -- "$@"
