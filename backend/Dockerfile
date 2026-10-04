# syntax=docker/dockerfile:1
# Production images, built from a clean checkout (see Dockerfile.dockerignore):
#   docker build --target web      -t dixels-api      backend    # the API + login pages
#   docker build --target migrator -t dixels-migrator backend    # applies migrations and seeds, then exits
# deploy/docker-compose.yml builds and runs both. Local development uses Dockerfile.dev.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Node and the ABP CLI are only here for `abp install-libs`: wwwroot/libs is git-ignored, and
# without it every request gets ABP's "Libs folder is missing" error page. Node 24 because
# one of the libs (select2) requires it. Same steps as the CI jobs.
RUN curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet tool install --global Volo.Abp.Studio.Cli --version 3.1.3

WORKDIR /src

# Restore in its own layer so a code change doesn't re-download every package.
COPY common.props NuGet.Config ./
COPY src/Volo.Abp.MultiLingualObjects/*.csproj src/Volo.Abp.MultiLingualObjects/
COPY src/Dixels.Domain.Shared/*.csproj src/Dixels.Domain.Shared/
COPY src/Dixels.Domain/*.csproj src/Dixels.Domain/
COPY src/Dixels.Application.Contracts/*.csproj src/Dixels.Application.Contracts/
COPY src/Dixels.Application/*.csproj src/Dixels.Application/
COPY src/Dixels.EntityFrameworkCore/*.csproj src/Dixels.EntityFrameworkCore/
COPY src/Dixels.HttpApi/*.csproj src/Dixels.HttpApi/
COPY src/Dixels.Web/*.csproj src/Dixels.Web/
COPY src/Dixels.DbMigrator/*.csproj src/Dixels.DbMigrator/
RUN dotnet restore src/Dixels.Web/Dixels.Web.csproj \
    && dotnet restore src/Dixels.DbMigrator/Dixels.DbMigrator.csproj

# Client libs, also cached until package.json / yarn.lock change.
COPY src/Dixels.Web/package.json src/Dixels.Web/yarn.lock src/Dixels.Web/abp.resourcemapping.js src/Dixels.Web/
RUN cd src/Dixels.Web && abp install-libs

COPY src/ src/
RUN dotnet publish src/Dixels.Web/Dixels.Web.csproj -c Release --no-restore -o /out/web \
    && dotnet publish src/Dixels.DbMigrator/Dixels.DbMigrator.csproj -c Release --no-restore -o /out/migrator


# The small runtime image (no SDK), shared by both targets so they share layers.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl for the HEALTHCHECK below; the image has no HTTP client of its own.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
# Serilog writes Logs/logs.txt next to the app, and /keys is the data protection volume;
# both must be writable by the non-root user the containers run as.
RUN mkdir -p /app/Logs /keys && chown "$APP_UID" /app/Logs /keys
ENV ASPNETCORE_ENVIRONMENT=Production


FROM runtime AS migrator
COPY --from=build /out/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "Dixels.DbMigrator.dll"]


FROM runtime AS web
COPY --from=build /out/web .
USER $APP_UID
# Plain HTTP inside the container network; TLS ends at the proxy in front
# (set ForwardedHeaders__Enabled=true so the app knows the request was https).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=60s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Dixels.Web.dll"]
