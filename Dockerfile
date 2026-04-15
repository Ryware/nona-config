FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NonaConfig.slnx ./
COPY core/src/Domain/Domain.csproj core/src/Domain/
COPY core/src/Application/Application.csproj core/src/Application/
COPY core/src/Infrastructure/Infrastructure.csproj core/src/Infrastructure/
COPY core/src/Libsql/Libsql.csproj core/src/Libsql/
COPY core/src/WebApi/WebApi.csproj core/src/WebApi/

RUN dotnet restore core/src/WebApi/WebApi.csproj

COPY core/src/ core/src/

RUN dotnet publish core/src/WebApi/WebApi.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /var/lib/nona

COPY --from=build /app/publish/ ./

EXPOSE 8080

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

ENTRYPOINT ["dotnet", "Nona.WebApi.dll"]

