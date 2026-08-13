# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY backend/backend.csproj ./backend/
RUN dotnet restore backend/backend.csproj

# Copy everything else and build website
COPY backend/ ./backend/
RUN dotnet publish backend/backend.csproj -c Release -o out

# Runtime Image Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "backend.dll"]
