# Etapa 1: Construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY WebApplication1/WebApplication1.csproj ./WebApplication1/
RUN dotnet restore ./WebApplication1/WebApplication1.csproj

COPY WebApplication1/. ./WebApplication1/
WORKDIR /app/WebApplication1
RUN dotnet publish -c Release -o out

# Etapa 2: Ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instalar MongoDB CLI (mongodump/mongorestore)
RUN apt-get update && \
    apt-get install -y mongodb-clients && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/WebApplication1/out ./
ENTRYPOINT ["dotnet", "WebApplication1.dll"]