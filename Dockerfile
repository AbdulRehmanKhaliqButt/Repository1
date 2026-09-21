FROM node:24-alpine AS web-build
WORKDIR /web

COPY src/IncidentAgent.Web/package.json ./
RUN npm install --no-audit --no-fund

COPY src/IncidentAgent.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/IncidentAgent.Core/IncidentAgent.Core.csproj src/IncidentAgent.Core/
COPY src/IncidentAgent.Infrastructure/IncidentAgent.Infrastructure.csproj src/IncidentAgent.Infrastructure/
COPY src/IncidentAgent.Persistence/IncidentAgent.Persistence.csproj src/IncidentAgent.Persistence/
COPY src/IncidentAgent.Api/IncidentAgent.Api.csproj src/IncidentAgent.Api/
RUN dotnet restore src/IncidentAgent.Api/IncidentAgent.Api.csproj

COPY src/ src/
RUN dotnet publish src/IncidentAgent.Api/IncidentAgent.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=web-build /web/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "IncidentAgent.Api.dll"]
