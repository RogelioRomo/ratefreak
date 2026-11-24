FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS build
WORKDIR /src
RUN apt-get update && apt-get install -y --no-install-recommends python3 && rm -rf /var/lib/apt/lists/*
COPY Directory.Build.props Directory.Build.targets VERSION global.json RateFreak.sln ./
ARG RATEFREAK_REVISION=unknown
ARG RATEFREAK_OFFICIAL=false
ENV RateFreakRevision=$RATEFREAK_REVISION RateFreakOfficial=$RATEFREAK_OFFICIAL
COPY .config ./.config
COPY src ./src
RUN dotnet restore src/RateFreak.Api/RateFreak.Api.csproj && dotnet tool restore
RUN dotnet publish src/RateFreak.Api/RateFreak.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false /p:RateFreakRevision=$RATEFREAK_REVISION /p:RateFreakOfficial=$RATEFREAK_OFFICIAL
RUN ConnectionStrings__Default="Host=127.0.0.1;Database=build_only;Username=build_only" Authentication__DataProtectionKeysPath=/tmp/build-keys dotnet ef migrations bundle --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api --configuration Release --output /out/efbundle
COPY LICENSE ./LICENSE
COPY deploy/compliance ./deploy/compliance
RUN python3 deploy/compliance/notices.py nuget /root/.nuget/packages /notices --assets src/RateFreak.Api/obj/project.assets.json --include-package dotnet-ef/10.0.2 --supplements deploy/compliance/supplements --project-license LICENSE --evidence deploy/compliance/native --check

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble@sha256:aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21 AS dotnet
FROM debian:bookworm-slim@sha256:734a72b3d6142a71993b4bc580ca01d8ca687f77a2c422d96a961d3376b179bc AS runtime
RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends python3 libicu72 ca-certificates && rm -rf /var/lib/apt/lists/*
COPY deploy/self-host/postgresql-signing-key.asc /usr/share/keyrings/ratefreak-postgresql.asc
COPY deploy/self-host/pgdg.sources /etc/apt/sources.list.d/pgdg.sources
# Maintenance needs psql/pg_dump/pg_restore, not a PostgreSQL server or gosu.
RUN apt-get update && apt-get install -y --no-install-recommends postgresql-client-17 && rm -rf /var/lib/apt/lists/*
COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/ratefreak-bundle DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production
WORKDIR /app
COPY --from=build /out/ ./
COPY --from=build /notices/ /app/notices/
COPY deploy/self-host/entrypoint.py /app/entrypoint.py
COPY deploy/self-host/maintenance.py /app/maintenance.py
COPY deploy/compliance/runtime_evidence.py /app/runtime_evidence.py
COPY deploy/compliance/runtime-supplements/sources.json /app/runtime-supplements/sources.json
COPY deploy/compliance/runtime-supplements/DOTNET-REDISTRIBUTION-NOTICES.txt deploy/compliance/runtime-supplements/SKIA-REDISTRIBUTION-NOTICES.txt /app/runtime-supplements/
RUN python3 /app/runtime_evidence.py /app/notices/runtime --image-family tools
ARG RATEFREAK_VERSION
ARG RATEFREAK_REVISION=unknown
LABEL org.opencontainers.image.version=$RATEFREAK_VERSION org.opencontainers.image.revision=$RATEFREAK_REVISION org.opencontainers.image.source="https://github.com/RogelioRomo/ratefreak"
USER 10001:10001
ENTRYPOINT ["python3", "/app/entrypoint.py"]
CMD ["replay"]
