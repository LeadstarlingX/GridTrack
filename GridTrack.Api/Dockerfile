# syntax=docker/dockerfile:1

# ------- 1. Runtime layer (smallest) -------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# ------- 2. Build / publish layer -------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# ---- copy files required for restore (cacheable) ----
COPY GridTrack.Api/global.json ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY *.sln ./
COPY */*.csproj ./

# Recreate folder structure for solution restore
RUN for f in *.csproj; do \
      mkdir -p "${f%.*}" && mv "$f" "${f%.*}/"; \
    done

# Persist the NuGet package cache across builds via a BuildKit cache mount.
# Without this, an invalidated restore layer re-downloads the whole graph from
# nuget.org every time — slow, and a single transient EOF stalls the build.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore GridTrack.Api

# copy everything else & build (publish re-resolves packages — same cache mount
# keeps it offline/fast instead of hitting nuget.org again)
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish GridTrack.Api/GridTrack.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ------- 3. Final stage -------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GridTrack.Api.dll"]
