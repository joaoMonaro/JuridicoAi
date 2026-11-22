FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /source
COPY src/JuridicoAI.Api/JuridicoAI.Api.csproj src/JuridicoAI.Api/
RUN dotnet restore src/JuridicoAI.Api/JuridicoAI.Api.csproj
COPY src/JuridicoAI.Api/ src/JuridicoAI.Api/
RUN dotnet publish src/JuridicoAI.Api/JuridicoAI.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    libreoffice-writer poppler-utils libfontconfig1 fonts-liberation fonts-noto-core curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/juridicoai/documents \
    && chown -R app:app /var/lib/juridicoai
WORKDIR /app
COPY --from=build /out/ ./
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080 HOME=/tmp/juridicoai-home
USER app
ENTRYPOINT ["dotnet", "JuridicoAI.Api.dll"]
