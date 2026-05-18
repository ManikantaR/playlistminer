# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/PlaylistMiner.Worker/PlaylistMiner.Worker.csproj", "src/PlaylistMiner.Worker/"]
COPY ["src/PlaylistMiner.Core/PlaylistMiner.Core.csproj", "src/PlaylistMiner.Core/"]
COPY ["src/PlaylistMiner.Infrastructure/PlaylistMiner.Infrastructure.csproj", "src/PlaylistMiner.Infrastructure/"]
COPY ["src/PlaylistMiner.ServiceDefaults/PlaylistMiner.ServiceDefaults.csproj", "src/PlaylistMiner.ServiceDefaults/"]
RUN dotnet restore "src/PlaylistMiner.Worker/PlaylistMiner.Worker.csproj"

COPY . .
RUN dotnet publish "src/PlaylistMiner.Worker/PlaylistMiner.Worker.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlaylistMiner.Worker.dll"]
