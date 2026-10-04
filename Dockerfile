FROM node:24-bookworm-slim AS css
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --include=dev
COPY scripts/tailwind-resolver.mjs ./scripts/tailwind-resolver.mjs
COPY src/ResellManager.Web/ ./src/ResellManager.Web/
RUN npm run css:build

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
COPY --from=css /src/src/ResellManager.Web/wwwroot/css/tailwind.css ./src/ResellManager.Web/wwwroot/css/tailwind.css
RUN dotnet restore src/ResellManager.Web/ResellManager.Web.csproj
RUN dotnet publish src/ResellManager.Web/ResellManager.Web.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
# Ubuntu/glibc matches SkiaSharp.NativeAssets.Linux.NoDependencies.
COPY --from=build /out/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ResellManager.Web.dll"]
