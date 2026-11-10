FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
COPY Directory.Build.props global.json RateFreak.sln ./
COPY .config ./.config
COPY src ./src
RUN dotnet restore src/RateFreak.Api/RateFreak.Api.csproj && dotnet tool restore
RUN dotnet publish src/RateFreak.Api/RateFreak.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false
RUN ConnectionStrings__Default="Host=127.0.0.1;Database=build_only;Username=build_only" Authentication__DataProtectionKeysPath=/tmp/build-keys dotnet ef migrations bundle --project src/RateFreak.Infrastructure --startup-project src/RateFreak.Api --configuration Release --output /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c AS dotnet
FROM postgres:17-bookworm@sha256:665dc7b37ac52a17e298511414c21d23ffa4e6a6ef6225b3e5d59c7afb35e994 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends python3 libicu72 && rm -rf /var/lib/apt/lists/*
COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/ratefreak-bundle DOTNET_ROOT=/usr/share/dotnet PATH=/usr/share/dotnet:$PATH ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production
WORKDIR /app
COPY --from=build /out/ ./
COPY deploy/self-host/entrypoint.py /app/entrypoint.py
COPY deploy/self-host/maintenance.py /app/maintenance.py
USER 10001:10001
ENTRYPOINT ["python3", "/app/entrypoint.py"]
CMD ["replay"]
