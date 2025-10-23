FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Define o diretório de trabalho como /app
WORKDIR /app

# Copia o arquivo .sln da raiz (uma pasta acima) e o .csproj do projeto
COPY ../TicketObras.sln ./
COPY ./Presentation.csproj ./

# Restaura as dependências da solução inteira
RUN dotnet restore TicketObras.sln

# Copia todo o código do projeto Presentation
COPY . ./

# Publica o projeto Presentation (não a solução inteira)
RUN dotnet publish -c Release -o out -p:AssemblyName=Presentation

# Etapa 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "Presentation.dll"]