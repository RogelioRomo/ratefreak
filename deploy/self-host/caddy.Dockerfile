# Copy the unchanged vendor's static Go server and serving configuration only.
# Alpine package-manager/helper layers are not part of the distributed runtime.
FROM caddy:2@sha256:ce773ec7a742ce15ee6bf2bb5f65882b9e2a4487b10733e6f1484f86e6b5d8bc AS vendor
FROM debian:trixie-slim@sha256:7378260c0a098d27ac206225db5e7d92b8c7c84ea7afccc86eeb513756bbd392 AS runtime
RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=vendor /usr/bin/caddy /usr/bin/caddy
COPY --from=vendor /etc/caddy/Caddyfile /etc/caddy/Caddyfile
COPY --from=vendor /usr/share/caddy/index.html /usr/share/caddy/index.html
ENV CADDY_VERSION=v2.11.9 XDG_CONFIG_HOME=/config XDG_DATA_HOME=/data GLIBC_TUNABLES=glibc.malloc.arena_max=1
WORKDIR /srv
EXPOSE 80 443 443/udp 2019
CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
ARG RATEFREAK_VERSION
ARG RATEFREAK_REVISION=unknown
LABEL org.opencontainers.image.version=$RATEFREAK_VERSION org.opencontainers.image.revision=$RATEFREAK_REVISION org.opencontainers.image.source="https://github.com/RogelioRomo/ratefreak"
