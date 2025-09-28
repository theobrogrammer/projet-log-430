# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy solution file
COPY ProjetLog430.sln .

# Copy all project files
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure.Web/Infrastructure.Web.csproj src/Infrastructure.Web/
COPY src/Infrastructure.Persistence/Infrastructure.Persistence.csproj src/Infrastructure.Persistence/
COPY src/Infrastructure.Adapters/Infrastructure.Adapters.csproj src/Infrastructure.Adapters/
COPY tests/Domain.Tests/Domain.Tests.csproj tests/Domain.Tests/
COPY tests/Application.Tests/Application.Tests.csproj tests/Application.Tests/
COPY tests/Infrastructure.Tests/Infrastructure.Tests.csproj tests/Infrastructure.Tests/
COPY tests/E2E.Tests/E2E.Tests.csproj tests/E2E.Tests/

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY src/ src/
COPY tests/ tests/

# Build the application
RUN dotnet build -c Release --no-restore

# Publish the web application
RUN dotnet publish src/Infrastructure.Web/Infrastructure.Web.csproj -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published application
COPY --from=build /app .

# Expose port 5000
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "Infrastructure.Web.dll"]