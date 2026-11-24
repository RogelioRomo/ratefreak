FROM node:24-bookworm-slim@sha256:dc26ae92c9fb15cf6b1d63e20f0944b2efe0f115b20e00be48fb23fceb061c54 AS build
WORKDIR /src/web
RUN apt-get update && apt-get install -y --no-install-recommends python3 && rm -rf /var/lib/apt/lists/*
RUN corepack enable && corepack prepare pnpm@10.28.2 --activate
COPY web/package.json web/pnpm-lock.yaml web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY web ./
COPY VERSION /src/VERSION
COPY .build-identity.json /src/.build-identity.json
ENV NEXT_TELEMETRY_DISABLED=1
COPY LICENSE /src/LICENSE
COPY deploy/compliance /src/deploy/compliance
RUN pnpm build

FROM debian:bookworm-slim@sha256:734a72b3d6142a71993b4bc580ca01d8ca687f77a2c422d96a961d3376b179bc AS runtime
RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends python3 ca-certificates libstdc++6 libatomic1 && rm -rf /var/lib/apt/lists/*
# Copy only Node and its complete composite license; package managers stay in build.
COPY --from=build /usr/local/bin/node /usr/local/bin/node
COPY --from=build /usr/local/LICENSE /usr/local/LICENSE
WORKDIR /app
COPY --from=build --chown=10001:10001 /src/web/.next/standalone ./
COPY --from=build --chown=10001:10001 /src/web/.next/static ./.next/static
COPY --from=build --chown=10001:10001 /src/web/public ./public
COPY deploy/self-host/entrypoint.py ./entrypoint.py
ENV NODE_ENV=production HOSTNAME=0.0.0.0 PORT=3000 NEXT_TELEMETRY_DISABLED=1
COPY deploy/compliance/runtime_evidence.py /app/runtime_evidence.py
COPY deploy/compliance/runtime-supplements/sources.json /app/runtime-supplements/sources.json
COPY deploy/compliance/runtime-supplements/NODE-REDISTRIBUTION-NOTICES.txt /app/runtime-supplements/
RUN python3 /app/runtime_evidence.py /app/public/notices/runtime --image-family web
ARG RATEFREAK_VERSION
ARG RATEFREAK_REVISION=unknown
LABEL org.opencontainers.image.version=$RATEFREAK_VERSION org.opencontainers.image.revision=$RATEFREAK_REVISION org.opencontainers.image.source="https://github.com/RogelioRomo/ratefreak"
USER 10001:10001
EXPOSE 3000
ENTRYPOINT ["python3", "/app/entrypoint.py", "web"]
