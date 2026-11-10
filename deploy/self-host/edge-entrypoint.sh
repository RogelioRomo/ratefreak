#!/bin/sh
set -eu
RATEFREAK_INGRESS_KEY="$(cat /run/ratefreak/ingress)"
export RATEFREAK_INGRESS_KEY
exec caddy run --config /etc/caddy/Caddyfile --adapter caddyfile
