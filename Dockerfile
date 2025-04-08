# Etapa 1: Construcción
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar y restaurar dependencias
COPY WebApplication1/WebApplication1.csproj ./WebApplication1/
RUN dotnet restore ./WebApplication1/WebApplication1.csproj

# Copiar y publicar aplicación
COPY WebApplication1/. ./WebApplication1/
WORKDIR /app/WebApplication1
RUN dotnet publish -c Release -o out

# Etapa 2: Ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Instalar MongoDB CLI (mongodump/mongorestore)
RUN apt-get update && \
    apt-get install -y wget gnupg && \
    wget -qO - https://www.mongodb.org/static/pgp/server-6.0.asc | apt-key add - && \
    echo "deb http://repo.mongodb.org/apt/debian bookworm/mongodb-org/6.0 main" | tee /etc/apt/sources.list.d/mongodb-org-6.0.list && \
    apt-get update && \
    apt-get install -y mongodb-database-tools && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copiar binarios publicados
COPY --from=build /app/WebApplication1/out ./

ENTRYPOINT ["dotnet", "WebApplication1.dll"]