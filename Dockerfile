# Base dotnet image v10.0
#  - using image digest only for immutability and security (see SonarCloud rule docker:S8431)
FROM mcr.microsoft.com/dotnet/aspnet@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS base
WORKDIR /app

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt update && \
    apt install curl -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Build stage image
#  - using image digest only for immutability and security (see SonarCloud rule docker:S8431)
FROM mcr.microsoft.com/dotnet/sdk@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src

COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY .csharpierrc .csharpierrc
COPY .editorconfig .editorconfig
COPY global.json global.json

RUN dotnet tool restore

COPY src/Api/Api.csproj src/Api/Api.csproj
COPY tests/Testing/Testing.csproj tests/Testing/Testing.csproj
COPY tests/Api.Tests/Api.Tests.csproj tests/Api.Tests/Api.Tests.csproj
COPY tests/Api.IntegrationTests/Api.IntegrationTests.csproj tests/Api.IntegrationTests/Api.IntegrationTests.csproj
COPY tests/Api.V1.Tests/Api.V1.Tests.csproj tests/Api.V1.Tests/Api.V1.Tests.csproj
COPY waste-organisations.slnx waste-organisations.slnx
COPY Directory.Build.props Directory.Build.props

RUN dotnet restore

COPY src/Api src/Api
COPY tests/Testing tests/Testing
COPY tests/Api.Tests tests/Api.Tests
COPY tests/Api.IntegrationTests tests/Api.IntegrationTests
COPY tests/Api.V1.Tests tests/Api.V1.Tests

RUN dotnet csharpier check .

RUN dotnet build --no-restore --warnaserror && \
    dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build

FROM build AS publish

RUN dotnet publish src/Api -c Release -warnaserror -o /app/publish /p:UseAppHost=false

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Final production image
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8085
ENTRYPOINT ["dotnet", "Api.dll"]
