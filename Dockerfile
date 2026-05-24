FROM node:22-alpine AS frontend-build
WORKDIR /frontend

COPY nona-config-admin/package.json nona-config-admin/package-lock.json ./
RUN npm ci

COPY nona-config-admin/ ./
ARG FRONTEND_API_URL=
RUN if [ -n "$FRONTEND_API_URL" ]; then \
        export VITE_API_URL="$FRONTEND_API_URL"; \
    else \
        export VITE_API_URL=""; \
        node -e "const fs=require('fs'); const p='src/services/api-client.ts'; const old='const API_BASE_URL = import.meta.env.VITE_API_URL || \"http://localhost:5027\";'; const next='const API_BASE_URL = import.meta.env.VITE_API_URL || window.location.origin;'; const source=fs.readFileSync(p,'utf8'); if(!source.includes(old)){throw new Error('API fallback not found')} fs.writeFileSync(p,source.replace(old,next));"; \
    fi; \
    npm exec vite -- build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY nona-config/NonaConfig.slnx ./
COPY nona-config/core/src/Domain/Domain.csproj core/src/Domain/
COPY nona-config/core/src/Application/Application.csproj core/src/Application/
COPY nona-config/core/src/Infrastructure/Infrastructure.csproj core/src/Infrastructure/
COPY nona-config/libsql/src/Libsql/Libsql.csproj libsql/src/Libsql/
COPY nona-config/core/src/WebApi/WebApi.csproj core/src/WebApi/

RUN dotnet restore core/src/WebApi/WebApi.csproj

COPY nona-config/core/src/ core/src/
COPY nona-config/libsql/src/ libsql/src/

RUN dotnet publish core/src/WebApi/WebApi.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

COPY --from=frontend-build /frontend/dist/ /app/publish/wwwroot/

RUN mkdir -p /empty-nona \
    && chmod -R a+rX /app/publish

FROM debian:bookworm-slim AS libsql
ARG LIBSQL_SERVER_VERSION=0.24.32

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl xz-utils \
    && rm -rf /var/lib/apt/lists/* \
    && curl --proto '=https' --tlsv1.2 -LsSf -o /tmp/libsql-server.tar.xz "https://github.com/tursodatabase/libsql/releases/download/libsql-server-v${LIBSQL_SERVER_VERSION}/libsql-server-x86_64-unknown-linux-gnu.tar.xz" \
    && mkdir -p /tmp/libsql-server \
    && tar -xJf /tmp/libsql-server.tar.xz -C /tmp/libsql-server \
    && install /tmp/libsql-server/libsql-server-x86_64-unknown-linux-gnu/sqld /usr/local/bin/sqld \
    && rm -rf /tmp/libsql-server /tmp/libsql-server.tar.xz /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app

COPY --from=libsql /usr/local/bin/sqld /usr/local/bin/sqld
COPY --from=libsql /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY --from=build --chown=1654:1654 /empty-nona /var/lib/nona
COPY --from=build /app/publish/ ./

EXPOSE 8080
EXPOSE 9080

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

ENTRYPOINT ["dotnet", "Nona.WebApi.dll"]
