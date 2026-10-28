# Base dotnet image v10.0
#  - using image digest only for immutability and security (see SonarCloud rule docker:S8431)
FROM mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca AS base
WORKDIR /app

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt update && \
    apt install curl -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Build stage image
#  - using image digest only for immutability and security (see SonarCloud rule docker:S8431)
FROM mcr.microsoft.com/dotnet/sdk@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
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
