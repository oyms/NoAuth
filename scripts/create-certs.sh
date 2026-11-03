#!/usr/bin/env bash
# Creates a locally trusted certificate for running NoAuth with HTTPS in docker compose.
# Requires mkcert: https://github.com/FiloSottile/mkcert
set -euo pipefail

cd "$(dirname "$0")/.."
mkdir -p certs

# Trust mkcert's root certificate on this machine (no-op if already done).
mkcert -install

# Valid for clients on the host (localhost) and in other containers (the service name noauth).
mkcert -pkcs12 -p12-file certs/noauth.pfx localhost noauth 127.0.0.1 ::1

# The root certificate, for containers calling NoAuth to trust.
cp "$(mkcert -CAROOT)/rootCA.pem" certs/rootCA.pem

# Readable by the non-root user in the container.
chmod 644 certs/noauth.pfx certs/rootCA.pem

echo "Created certs/noauth.pfx (password: changeit) and certs/rootCA.pem"
