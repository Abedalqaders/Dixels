# syntax=docker/dockerfile:1
# Production image: the built SPA served by nginx (non-root, port 8080).
# Vite bakes VITE_* values into the bundle at build time, so they are build args:
#   docker build --build-arg VITE_API_BASE_URL=https://api.example.com \
#                --build-arg VITE_OIDC_AUTHORITY=https://api.example.com \
#                --build-arg VITE_OIDC_REDIRECT_URI=https://app.example.com/callback \
#                --build-arg VITE_OIDC_POST_LOGOUT_REDIRECT_URI=https://app.example.com/ \
#                -t dixels-app frontend
# deploy/docker-compose.yml passes them from deploy/.env.

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
ARG VITE_API_BASE_URL
ARG VITE_OIDC_AUTHORITY
ARG VITE_OIDC_REDIRECT_URI
ARG VITE_OIDC_POST_LOGOUT_REDIRECT_URI
ARG VITE_OIDC_CLIENT_ID=Dixels_App
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
