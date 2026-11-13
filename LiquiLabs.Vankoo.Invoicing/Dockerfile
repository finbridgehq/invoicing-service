# =============================================================================
# STAGE 1: base
# Runtime limpio sin SDK. Se reutiliza en el stage final para mantener
# la imagen pequeña. El usuario sin privilegios reduce la superficie de ataque.
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
# La imagen base ya setea ASPNETCORE_HTTP_PORTS=8080 por defecto

# =============================================================================
# STAGE 2: build
# SDK completo para restaurar dependencias y compilar. No llega a la imagen final.
# El .csproj se copia antes que el código para aprovechar la caché de capas:
# si las dependencias no cambian, "dotnet restore" no se vuelve a ejecutar.
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
# Sobreescribible con --build-arg BUILD_CONFIGURATION=Debug
WORKDIR /src
COPY ["LiquiLabs.Vankoo.Invoicing/LiquiLabs.Vankoo.Invoicing.csproj", "LiquiLabs.Vankoo.Invoicing/"]
RUN dotnet restore "LiquiLabs.Vankoo.Invoicing/LiquiLabs.Vankoo.Invoicing.csproj"
COPY . .
WORKDIR "/src/LiquiLabs.Vankoo.Invoicing"
RUN dotnet build "./LiquiLabs.Vankoo.Invoicing.csproj" -c $BUILD_CONFIGURATION -o /app/build

# =============================================================================
# STAGE 3: publish
# Genera los artefactos de producción. UseAppHost=false evita generar un
# ejecutable nativo del SO — en Linux se usa directamente el runtime de .NET.
# =============================================================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./LiquiLabs.Vankoo.Invoicing.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# =============================================================================
# STAGE 4: final
# Imagen de producción: runtime limpio + artefactos publicados.
# El SDK y el código fuente no están presentes.
# curl se instala para que Docker pueda ejecutar el healthcheck del compose.
# =============================================================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
USER $APP_UID
ENTRYPOINT ["dotnet", "LiquiLabs.Vankoo.Invoicing.dll"]
