ARG PORT=8085

# Build stage image
#  - Defra .NET 10 development image, as in the CDP .NET backend template
#  - tag and digest written out rather than through an ARG, so Dependabot can update them
#    (it does not resolve ARGs in FROM lines); the -dotnet10.0 suffix keeps it on .NET 10
FROM defradigital/dotnetcore-development:2.1.9-dotnet10.0@sha256:fb7eec2d2d2f68b6290348a30d861c577003a2d85b0de6d7d6e38379104e4a51 AS build

# The image runs as the non-root dotnet user. Sources go in their own folder, not straight into
#   /home/dotnet, so that csharpier does not check the NuGet cache and tools stored there.
WORKDIR /home/dotnet/src

COPY --chown=dotnet:dotnet .config/dotnet-tools.json .config/dotnet-tools.json
COPY --chown=dotnet:dotnet .csharpierrc .csharpierrc
COPY --chown=dotnet:dotnet .editorconfig .editorconfig
COPY --chown=dotnet:dotnet global.json global.json

RUN dotnet tool restore

COPY --chown=dotnet:dotnet src/Api/Api.csproj src/Api/Api.csproj
COPY --chown=dotnet:dotnet tests/Testing/Testing.csproj tests/Testing/Testing.csproj
COPY --chown=dotnet:dotnet tests/Api.Tests/Api.Tests.csproj tests/Api.Tests/Api.Tests.csproj
COPY --chown=dotnet:dotnet tests/Api.IntegrationTests/Api.IntegrationTests.csproj tests/Api.IntegrationTests/Api.IntegrationTests.csproj
COPY --chown=dotnet:dotnet tests/Api.V1.Tests/Api.V1.Tests.csproj tests/Api.V1.Tests/Api.V1.Tests.csproj
COPY --chown=dotnet:dotnet waste-organisations.slnx waste-organisations.slnx
COPY --chown=dotnet:dotnet Directory.Build.props Directory.Build.props

RUN dotnet restore

COPY --chown=dotnet:dotnet src/Api src/Api
COPY --chown=dotnet:dotnet tests/Testing tests/Testing
COPY --chown=dotnet:dotnet tests/Api.Tests tests/Api.Tests
COPY --chown=dotnet:dotnet tests/Api.IntegrationTests tests/Api.IntegrationTests
COPY --chown=dotnet:dotnet tests/Api.V1.Tests tests/Api.V1.Tests

RUN dotnet csharpier check .

RUN dotnet build --no-restore --warnaserror && \
    dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build

FROM build AS publish

RUN dotnet publish src/Api -c Release -warnaserror -o /home/dotnet/publish /p:UseAppHost=false

# Final production image
#  - Defra .NET 10 runtime image (Alpine); pinned as for the build stage, at the same version
FROM defradigital/dotnetcore:2.1.9-dotnet10.0@sha256:5277a59c73643588d3555ac4ca340de8a2e9500d9e37ebef4ba9c3049bb12a15 AS production

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
USER root
RUN apk add --no-cache curl icu-data-full icu-libs

# The Alpine base defaults to invariant globalization; keep real cultures (en-GB, cy-GB) as on the old Debian image.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# App files stay root-owned so the dotnet user can't modify them. Not under /home/dotnet,
#   which must stay writable: TrustStore.cs imports TRUSTSTORE_* certs into ~/.dotnet.
#   COPY --from keeps the build stage's dotnet ownership, so set root explicitly.
WORKDIR /app
COPY --from=publish --chown=root:root /home/dotnet/publish .
USER dotnet

ARG PORT
ENV ASPNETCORE_HTTP_PORTS=${PORT}
EXPOSE ${PORT}
ENTRYPOINT ["dotnet", "Api.dll"]
