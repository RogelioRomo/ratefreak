FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
RUN apt-get update && apt-get install -y --no-install-recommends python3 && rm -rf /var/lib/apt/lists/*
COPY Directory.Build.props Directory.Build.targets VERSION global.json RateFreak.sln ./
ARG RATEFREAK_REVISION=unknown
ARG RATEFREAK_OFFICIAL=false
ENV RateFreakRevision=$RATEFREAK_REVISION RateFreakOfficial=$RATEFREAK_OFFICIAL
COPY src ./src
RUN dotnet restore src/RateFreak.Api/RateFreak.Api.csproj
RUN dotnet publish src/RateFreak.Api/RateFreak.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false /p:RateFreakRevision=$RATEFREAK_REVISION /p:RateFreakOfficial=$RATEFREAK_OFFICIAL
COPY LICENSE ./LICENSE
COPY deploy/compliance ./deploy/compliance
RUN python3 deploy/compliance/notices.py nuget /root/.nuget/packages /notices --assets src/RateFreak.Api/obj/project.assets.json --supplements deploy/compliance/supplements --project-license LICENSE --evidence deploy/compliance/native --check

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends python3 curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/ ./
COPY --from=build /notices/ /app/notices/
COPY deploy/self-host/entrypoint.py /app/entrypoint.py
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production
COPY deploy/compliance/runtime_evidence.py /app/runtime_evidence.py
RUN python3 /app/runtime_evidence.py /app/notices/runtime
ARG RATEFREAK_VERSION
ARG RATEFREAK_REVISION=unknown
LABEL org.opencontainers.image.version=$RATEFREAK_VERSION org.opencontainers.image.revision=$RATEFREAK_REVISION org.opencontainers.image.source="https://github.com/RogelioRomo/ratefreak"
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["python3", "/app/entrypoint.py", "api"]
