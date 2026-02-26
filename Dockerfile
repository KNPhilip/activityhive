FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app
EXPOSE 8080

# Copy .fsproj files and restore as distinct layers
COPY "ActivityHive.sln" "ActivityHive.sln"
COPY "API/API.fsproj" "API/API.fsproj"
COPY "Application/Application.fsproj" "Application/Application.fsproj"
COPY "Persistence/Persistence.fsproj" "Persistence/Persistence.fsproj"
COPY "Domain/Domain.fsproj" "Domain/Domain.fsproj"
COPY "Infrastructure/Infrastructure.fsproj" "Infrastructure/Infrastructure.fsproj"

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