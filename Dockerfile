FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY ./publish/ ./

ENV ASPNETCORE_URLS=http://0.0.0.0:3830

EXPOSE 3830

ENTRYPOINT ["dotnet", "TranslationApi.dll"]
daniil@ravix /opt/whisper % 