# Multi-stage build producing two images from one shared build: the API and the
# worker. Select with `--target api` / `--target worker` (the compose file does
# this per service).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, against just the project/solution files, so the layer caches
# until dependencies actually change.
COPY *.slnx global.json Directory.Build.props ./
COPY src/CaseFlow.Api/*.csproj src/CaseFlow.Api/
COPY src/CaseFlow.Application/*.csproj src/CaseFlow.Application/
COPY src/CaseFlow.Domain/*.csproj src/CaseFlow.Domain/
COPY src/CaseFlow.Infrastructure/*.csproj src/CaseFlow.Infrastructure/
COPY src/CaseFlow.Contracts/*.csproj src/CaseFlow.Contracts/
COPY src/CaseFlow.Worker/*.csproj src/CaseFlow.Worker/
COPY tests/CaseFlow.UnitTests/*.csproj tests/CaseFlow.UnitTests/
COPY tests/CaseFlow.IntegrationTests/*.csproj tests/CaseFlow.IntegrationTests/
COPY tests/CaseFlow.ArchitectureTests/*.csproj tests/CaseFlow.ArchitectureTests/
RUN dotnet restore src/CaseFlow.Api/CaseFlow.Api.csproj \
 && dotnet restore src/CaseFlow.Worker/CaseFlow.Worker.csproj

COPY . .
RUN dotnet publish src/CaseFlow.Api/CaseFlow.Api.csproj -c Release -o /app/api --no-restore \
 && dotnet publish src/CaseFlow.Worker/CaseFlow.Worker.csproj -c Release -o /app/worker --no-restore

# --- API image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
# Npgsql probes for Kerberos (libgssapi) during auth negotiation; the slim
# image lacks it and logs a noisy error before falling back to password auth.
# Installing it keeps the startup log clean.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/api ./
# The aspnet images ship a non-root 'app' user; run as it.
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CaseFlow.Api.dll"]

# --- Worker image ---
# Uses the ASP.NET Core runtime, not the bare runtime: the worker references
# Hangfire.AspNetCore (for the server + recurring-job registration), which
# depends on the Microsoft.AspNetCore.App shared framework.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/worker ./
USER app
ENTRYPOINT ["dotnet", "CaseFlow.Worker.dll"]
