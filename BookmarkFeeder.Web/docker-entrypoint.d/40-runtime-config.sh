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

# Each value is pasted raw into a JS string literal below, so each is held to a character allow-list
# that cannot close the string or start a new line: a quote, backslash or newline would let a bad
# .env inject script into every page. Fail loudly rather than serve a broken or hostile config.
#
# require_only NAME VALUE CHARS HINT - fails unless VALUE is empty or uses only CHARS (the inside of
# a bracket expression). A case pattern tests the whole value; `printf | grep` would test it line
# by line and pass a value whose FIRST line was fine.
require_only() {
  case "$2" in
    *[!$3]*)
      echo "40-runtime-config: $1 $4" >&2
      exit 1
      ;;
  esac
}

# The domain also goes into the Content-Security-Policy header below, so it must be a bare hostname:
# anything else could break out of the header too, or serve a policy that silently blocks sign-in.
require_only VITE_AUTH0_DOMAIN "${VITE_AUTH0_DOMAIN:-}" 'A-Za-z0-9.-' \
  "must be a bare hostname such as tenant.eu.auth0.com"
require_only VITE_AUTH0_CLIENT_ID "${VITE_AUTH0_CLIENT_ID:-}" 'A-Za-z0-9_-' \
  "may contain only letters, digits, '_' and '-' (copy it from the Auth0 application settings)"
require_only VITE_AUTH0_AUDIENCE "${VITE_AUTH0_AUDIENCE:-}" 'A-Za-z0-9:/._~%-' \
  "must be a URL-style identifier such as https://bookmarkfeeder.example.com/api, with no quotes, spaces or backslashes"

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
