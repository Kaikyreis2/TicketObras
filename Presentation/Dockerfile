# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia tudo e restaura dependências
COPY . .
WORKDIR /src/Presentation
RUN dotnet restore

# Compila e publica em modo Release
RUN dotnet publish -c Release -o /app/publish

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expõe a porta padrão do ASP.NET
EXPOSE 8080

# Define a variável de ambiente padrão
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Inicia o aplicativo
ENTRYPOINT ["dotnet", "Presentation.dll"]
