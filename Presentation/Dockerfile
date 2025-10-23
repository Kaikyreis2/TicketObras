
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Copia o arquivo .sln da raiz (se estiver no contexto)
COPY TicketObras.sln ./

# Copia o .csproj do projeto Presentation (ajustado para o contexto)
COPY Presentation/Presentation.csproj ./Presentation/

# Restaura a solução inteira
RUN dotnet restore TicketObras.sln

# Copia todo o código do projeto Presentation
COPY Presentation/ ./Presentation/

# Publica o projeto Presentation
RUN dotnet publish Presentation/Presentation.csproj -c Release -o out

# Etapa 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Copia a saída da etapa de build
COPY --from=build /app/out ./

ENTRYPOINT ["dotnet", "Presentation.dll"]