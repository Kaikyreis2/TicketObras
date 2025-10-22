# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copia e restaura o projeto
COPY *.csproj ./
RUN dotnet restore

# Copia todo o resto e publica
COPY . .
RUN dotnet publish -c Release -o out

# Etapa final (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Presentation.dll"]