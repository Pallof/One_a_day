# How Fly.io builds and runs Stumpty. Fly builds this on its own servers (the default for
# `fly deploy`), so Docker doesn't need to be installed locally. See PRD 11 and the README's
# "Deploying to Fly.io".

# 1. Build: compile and publish with the full .NET SDK.
#
# Copy everything before restoring. The SDK only fetches Blazor's own script (blazor.web.js)
# if it can see .razor files when it restores, so the usual Docker trick of restoring from the
# .csproj alone built a site whose buttons did nothing (found on the first deploy, 2026-09-30).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY OneADay/ OneADay/
RUN dotnet publish OneADay/OneADay.csproj -c Release -o /app

# 2. Run: only the ASP.NET runtime and the published app, a much smaller image. App_Data is
# never in it — the project leaves it out of every publish (PRD 10), and on Fly that folder
# is the volume that fly.toml mounts.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY deploy/start.sh /app/start.sh

# The runtime image already listens on 8080, which fly.toml's internal_port matches. It runs
# as root, which the volume needs: Fly mounts it owned by root, and every save goes there.
ENTRYPOINT ["sh", "/app/start.sh"]
