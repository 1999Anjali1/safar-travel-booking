# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless a project file changes)
COPY Directory.Build.props ./
COPY src/Safar.Domain/Safar.Domain.csproj src/Safar.Domain/
COPY src/Safar.Application/Safar.Application.csproj src/Safar.Application/
COPY src/Safar.Infrastructure/Safar.Infrastructure.csproj src/Safar.Infrastructure/
COPY src/Safar.Api/Safar.Api.csproj src/Safar.Api/
RUN dotnet restore src/Safar.Api/Safar.Api.csproj

COPY src/ src/
RUN dotnet publish src/Safar.Api/Safar.Api.csproj -c Release -o /app --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Safar.Api.dll"]
