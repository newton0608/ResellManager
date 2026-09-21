FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet restore src/ResellManager.Web/ResellManager.Web.csproj
RUN dotnet publish src/ResellManager.Web/ResellManager.Web.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app
# Debian/glibc matches SkiaSharp.NativeAssets.Linux.NoDependencies.
COPY --from=build /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ResellManager.Web.dll"]
