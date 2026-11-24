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

# The domain also goes into the Content-Security-Policy header below, so it must be a bare hostname:
# anything else could break out of the header (or the JS string). Fail loudly rather than serve a
# policy that silently blocks sign-in.
if [ -n "${VITE_AUTH0_DOMAIN:-}" ] && ! printf '%s' "$VITE_AUTH0_DOMAIN" | grep -Eq '^[A-Za-z0-9.-]+$'; then
  echo "40-runtime-config: VITE_AUTH0_DOMAIN must be a bare hostname such as tenant.eu.auth0.com" >&2
  exit 1
fi

cat > /usr/share/nginx/html/config.js <<EOF
window.__BOOKMARKFEEDER_CONFIG__ = {
  auth0Domain: "${VITE_AUTH0_DOMAIN:-}",
  auth0ClientId: "${VITE_AUTH0_CLIENT_ID:-}",
  auth0Audience: "${VITE_AUTH0_AUDIENCE:-}"
};
EOF

# Security headers, with the tenant allowed in connect-src: auth0-spa-js exchanges codes and
# refresh tokens with it by fetch(). Login itself is a top-level redirect, and the iframe fallback
# is disabled (useRefreshTokensFallback={false}), so no frame-src is needed.
AUTH0_ORIGIN=""
if [ -n "${VITE_AUTH0_DOMAIN:-}" ]; then
  AUTH0_ORIGIN=" https://${VITE_AUTH0_DOMAIN}"
fi
sed "s|connect-src 'self'|connect-src 'self'${AUTH0_ORIGIN}|" \
  /etc/nginx/bookmarkfeeder/security-headers.default.conf \
  > /etc/nginx/bookmarkfeeder/security-headers.conf
