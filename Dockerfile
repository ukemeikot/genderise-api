# Multi-stage build for optimized image size
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy the solution and project files
COPY ["HNG-BACKEND/hng-task-one/hng-task-one.sln", "hng-task-one.sln"]
COPY ["HNG-BACKEND/hng-task-one/src/HngStageOne.Api/HngStageOne.Api.csproj", "src/HngStageOne.Api/"]
COPY ["HNG-BACKEND/hng-task-one/tests/HngStageOne.Api.Tests/HngStageOne.Api.Tests.csproj", "tests/HngStageOne.Api.Tests/"]

# Restore dependencies
RUN dotnet restore "hng-task-one.sln"

# Copy the complete source code
COPY ["HNG-BACKEND/hng-task-one/", "."]

# Build the application
RUN dotnet build "hng-task-one.sln" -c Release -o /app/build

# Publish the application
RUN dotnet publish "src/HngStageOne.Api/HngStageOne.Api.csproj" -c Release -o /app/publish

# Runtime stage - use only ASP.NET Core runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Install sqlite3 package for runtime (small footprint)
RUN apt-get update && apt-get install -y sqlite3 && rm -rf /var/lib/apt/lists/*

# Create data directory for SQLite persistence
RUN mkdir -p /app/data && chmod 777 /app/data

# Copy published applicaton from build stage
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet --version || exit 1

# Run the application
ENTRYPOINT ["dotnet", "HngStageOne.Api.dll"]
