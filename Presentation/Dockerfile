# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
WORKDIR /app
COPY Presentation/*.csproj ./      # copia o csproj da pasta Presentation
RUN dotnet restore
COPY Presentation/. ./             # copia o restante do projeto
RUN dotnet publish -c Release -o out
EXPOSE 8080
ENTRYPOINT ["dotnet", "Presentation.dll"]