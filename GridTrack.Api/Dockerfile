# ------- 1. Runtime layer (smallest) -------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-jammy-chiseled AS base
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

RUN dotnet restore GridTrack.Api

# copy everything else & build
COPY . .
RUN dotnet publish GridTrack.Api/GridTrack.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ------- 3. Final stage -------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GridTrack.Api.dll"]
