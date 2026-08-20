#!/bin/sh
# Writes the Auth0 coordinates into the served bundle at CONTAINER START.
#
# Why not bake them with Vite: `npm run build` runs inside the image (see Dockerfile), and the
# resulting image is published once to Docker Hub and pulled by every self-hoster - each with a
# different Auth0 tenant. Baking would force everyone to rebuild from source.
#
# Nothing here is secret. An OAuth public client id, the tenant domain and the API audience are all
# published in any SPA bundle by design.
set -eu

cat > /usr/share/nginx/html/config.js <<EOF
window.__BOOKMARKFEEDER_CONFIG__ = {
  auth0Domain: "${VITE_AUTH0_DOMAIN:-}",
  auth0ClientId: "${VITE_AUTH0_CLIENT_ID:-}",
  auth0Audience: "${VITE_AUTH0_AUDIENCE:-}"
};
EOF
