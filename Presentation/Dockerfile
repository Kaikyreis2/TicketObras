
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Copia o arquivo .sln da raiz (se estiver no contexto)
COPY .. .

# Restaura a solução inteira
RUN dotnet restore TicketObras.sln

RUN dotnet publish Presentation/Presentation.csproj -c Release -o out

# Etapa 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Copia a saída da etapa de build
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "Presentation.dll"]