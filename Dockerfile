# https://mcr.microsoft.com/en-us/artifact/mar/dotnet/sdk
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /KanbanCord

COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["Directory.Packages.props", "Directory.Packages.props"]
COPY ["KanbanCord.Bot/KanbanCord.Bot.csproj", "KanbanCord.Bot/"]
COPY ["KanbanCord.Bot/packages.lock.json", "KanbanCord.Bot/"]
COPY ["KanbanCord.Core/KanbanCord.Core.csproj", "KanbanCord.Core/"]
COPY ["KanbanCord.Core/packages.lock.json", "KanbanCord.Core/"]

RUN dotnet restore "KanbanCord.Bot/KanbanCord.Bot.csproj" --locked-mode

COPY ["KanbanCord.Bot/", "KanbanCord.Bot/"]
COPY ["KanbanCord.Core/", "KanbanCord.Core/"]

ARG application_version=0.0.0

RUN dotnet publish "KanbanCord.Bot/KanbanCord.Bot.csproj" \
    --no-restore \
    --configuration Release \
    --output /publish \
    -p:Version=$application_version

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app
EXPOSE 5000

COPY --from=build --chown=$APP_UID:$APP_UID /publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "KanbanCord.Bot.dll"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s \
    CMD wget --quiet --tries=1 --spider http://127.0.0.1:5000/health || exit 1
