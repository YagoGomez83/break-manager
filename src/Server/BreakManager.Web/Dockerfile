# syntax=docker/dockerfile:1
# Imagen del servidor BreakManager (API + Blazor). Contexto de build: raíz del repositorio.
#   docker build -f src/Server/BreakManager.Web/Dockerfile -t breakmanager-web .
# Si la red intercepta TLS, se puede pasar una CA raíz extra SOLO a la etapa de build:
#   docker build --secret id=extra_ca,src=/ruta/ca.crt ...

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

RUN --mount=type=secret,id=extra_ca,required=false \
    if [ -s /run/secrets/extra_ca ]; then \
      cp /run/secrets/extra_ca /usr/local/share/ca-certificates/extra-ca.crt && update-ca-certificates; \
    fi

# Restore en una capa propia para aprovechar la caché.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Server/BreakManager.Domain/BreakManager.Domain.csproj src/Server/BreakManager.Domain/
COPY src/Server/BreakManager.Application/BreakManager.Application.csproj src/Server/BreakManager.Application/
COPY src/Server/BreakManager.Infrastructure/BreakManager.Infrastructure.csproj src/Server/BreakManager.Infrastructure/
COPY src/Server/BreakManager.Web/BreakManager.Web.csproj src/Server/BreakManager.Web/
COPY src/Shared/BreakManager.Contracts/BreakManager.Contracts.csproj src/Shared/BreakManager.Contracts/
RUN dotnet restore src/Server/BreakManager.Web/BreakManager.Web.csproj

COPY src/Server/ src/Server/
COPY src/Shared/ src/Shared/
RUN dotnet publish src/Server/BreakManager.Web/BreakManager.Web.csproj \
      --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Los archivos quedan de root y solo lectura para el usuario de la aplicación.
COPY --from=build /app/publish .

# Usuario no root provisto por la imagen base (UID 1654, "app").
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "BreakManager.Web.dll"]
