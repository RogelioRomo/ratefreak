FROM node:24-bookworm-slim@sha256:01d566e8d94d8c619591d78652babfccd3c6992a3dffa9ed5ab39e11cf6c70e9 AS build
WORKDIR /src
RUN corepack enable && corepack prepare pnpm@10.28.2 --activate
COPY web/package.json web/pnpm-lock.yaml web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY web ./
ENV NEXT_TELEMETRY_DISABLED=1
RUN pnpm build

FROM node:24-bookworm-slim@sha256:01d566e8d94d8c619591d78652babfccd3c6992a3dffa9ed5ab39e11cf6c70e9 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends python3 ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=10001:10001 /src/.next/standalone ./
COPY --from=build --chown=10001:10001 /src/.next/static ./.next/static
COPY --from=build --chown=10001:10001 /src/public ./public
COPY deploy/self-host/entrypoint.py ./entrypoint.py
ENV NODE_ENV=production HOSTNAME=0.0.0.0 PORT=3000 NEXT_TELEMETRY_DISABLED=1
USER 10001:10001
EXPOSE 3000
ENTRYPOINT ["python3", "/app/entrypoint.py", "web"]
