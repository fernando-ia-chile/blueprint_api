# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar manifiestos primero para aprovechar la cache de restore.
COPY ["global.json", "./"]
COPY ["ApiBlueprint.sln", "./"]
COPY ["AIEP.Application.Api.Blueprint/AIEP.Application.Api.Blueprint.csproj", "AIEP.Application.Api.Blueprint/"]
COPY ["AIEP.Application.Logic/AIEP.Application.Logic.csproj", "AIEP.Application.Logic/"]
COPY ["AIEP.Application.Repository/AIEP.Application.Repository.csproj", "AIEP.Application.Repository/"]
COPY ["AIEP.Application.Model/AIEP.Application.Model.csproj", "AIEP.Application.Model/"]
COPY ["AIEP.Domain.DataBlueprint/AIEP.Domain.DataBlueprint.csproj", "AIEP.Domain.DataBlueprint/"]
COPY ["AIEP.Test/AIEP.Test.csproj", "AIEP.Test/"]
RUN dotnet restore "ApiBlueprint.sln"

COPY . .
RUN dotnet publish "AIEP.Application.Api.Blueprint/AIEP.Application.Api.Blueprint.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

EXPOSE 8080
COPY --from=build /app/publish .

# Usuario no-root que ya trae la imagen aspnet:10.0.
USER $APP_UID

ENTRYPOINT ["dotnet", "AIEP.Application.Api.Blueprint.dll"]
