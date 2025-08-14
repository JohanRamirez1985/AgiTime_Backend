# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copiar proyectos desde el contexto raíz
COPY ./Compartido ./Compartido
COPY ./Productos.API ./Productos.API

WORKDIR /src/Productos.API
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Etapa final
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "Productos.API.dll"]