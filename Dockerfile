FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app
EXPOSE 8080

# Copy .fsproj files and restore as distinct layers
COPY "ActivityHive.sln" "ActivityHive.sln"
COPY "src/API/API.fsproj" "src/API/API.fsproj"
COPY "src/Application/Application.fsproj" "src/Application/Application.fsproj"
COPY "src/Persistence/Persistence.fsproj" "src/Persistence/Persistence.fsproj"
COPY "src/Domain/Domain.fsproj" "src/Domain/Domain.fsproj"
COPY "src/Infrastructure/Infrastructure.fsproj" "src/Infrastructure/Infrastructure.fsproj"
COPY "tests/UnitTests/UnitTests.fsproj" "tests/UnitTests/UnitTests.fsproj"
COPY "tests/IntegrationTests/IntegrationTests.fsproj" "tests/IntegrationTests/IntegrationTests.fsproj"

RUN dotnet restore "ActivityHive.sln"

# Copy everything else and build
COPY . .
WORKDIR /app
RUN dotnet publish -c Release -o out

# Build a runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT [ "dotnet", "API.dll" ]
