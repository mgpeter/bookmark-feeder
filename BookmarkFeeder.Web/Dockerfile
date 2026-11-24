# Base images are pinned by digest (the tag is kept for readers): a floating tag rebuilds the same
# commit into a different image whenever upstream publishes, so "which build is running?" would
# have no stable answer. Bump the digests deliberately, e.g. with Dependabot or
# `docker buildx imagetools inspect node:22-alpine`.

# Stage 1 — build the Vite app (node 22.23.5)
FROM node:22-alpine@sha256:1314fb30ec37592d5845e5a160d522c0e680972aa534549940b8d73bde88481a AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Stage 2 — serve the static build with nginx (SPA fallback)
# (nginx 1.31.10)
FROM nginx:alpine@sha256:b50897d5c7b1aa08067c3d90c5e8b3e47ec4e3c91c91446f67820228f7b41e34
COPY nginx.conf /etc/nginx/conf.d/default.conf
# The baked default, plus a copy the entrypoint rewrites with the Auth0 tenant (see the script).
COPY nginx/security-headers.conf /etc/nginx/bookmarkfeeder/security-headers.default.conf
COPY nginx/security-headers.conf /etc/nginx/bookmarkfeeder/security-headers.conf
# nginx:alpine runs every *.sh in /docker-entrypoint.d/ before starting. This one writes config.js
# from the environment, which is how a self-hoster points the prebuilt image at their own Auth0
# tenant without rebuilding it.
COPY docker-entrypoint.d/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
RUN chmod +x /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build /app/dist /usr/share/nginx/html
# 8000 matches nginx.conf and the port Aspire assigns the web resource.
EXPOSE 8000
