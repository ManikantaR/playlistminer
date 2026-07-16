# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["src/PlaylistMiner.Api/PlaylistMiner.Api.csproj", "src/PlaylistMiner.Api/"]
COPY ["src/PlaylistMiner.Core/PlaylistMiner.Core.csproj", "src/PlaylistMiner.Core/"]
COPY ["src/PlaylistMiner.Infrastructure/PlaylistMiner.Infrastructure.csproj", "src/PlaylistMiner.Infrastructure/"]
COPY ["src/PlaylistMiner.ServiceDefaults/PlaylistMiner.ServiceDefaults.csproj", "src/PlaylistMiner.ServiceDefaults/"]
RUN dotnet restore "src/PlaylistMiner.Api/PlaylistMiner.Api.csproj"

COPY . .
RUN dotnet publish "src/PlaylistMiner.Api/PlaylistMiner.Api.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl is needed for the Docker healthcheck (GET /api/health) on the NAS stack.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlaylistMiner.Api.dll"]
