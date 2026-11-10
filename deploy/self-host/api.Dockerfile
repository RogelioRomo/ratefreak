FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
COPY Directory.Build.props global.json RateFreak.sln ./
COPY src ./src
RUN dotnet restore src/RateFreak.Api/RateFreak.Api.csproj
RUN dotnet publish src/RateFreak.Api/RateFreak.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends python3 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
COPY deploy/self-host/entrypoint.py /app/entrypoint.py
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["python3", "/app/entrypoint.py", "api"]
