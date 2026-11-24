# Stage 1 — build the Vite app
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Stage 2 — serve the static build with nginx (SPA fallback)
FROM nginx:alpine
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
