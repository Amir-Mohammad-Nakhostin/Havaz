FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Havaz.sln backend/
COPY backend/Havaz.Api/Havaz.Api.csproj backend/Havaz.Api/
COPY backend/Havaz.Application/Havaz.Application.csproj backend/Havaz.Application/
COPY backend/Havaz.Domain/Havaz.Domain.csproj backend/Havaz.Domain/
COPY backend/Havaz.Infrastructure/Havaz.Infrastructure.csproj backend/Havaz.Infrastructure/
RUN dotnet restore backend/Havaz.Api/Havaz.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/Havaz.Api/Havaz.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Havaz.Api.dll"]
