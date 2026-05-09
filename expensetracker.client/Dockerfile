## Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration=production

## Stage 2: Serve with nginx
FROM nginx:alpine

# Copy built assets (Angular 19 outputs to browser/)
COPY --from=build /app/dist/expensetracker.client/browser /usr/share/nginx/html

# nginx config for SPA routing
RUN printf 'server {\n\
  listen 4200;\n\
  root /usr/share/nginx/html;\n\
  index index.html;\n\
  location / {\n\
    try_files $uri $uri/ /index.html;\n\
  }\n\
  location ~* \\.(js|css|png|jpg|jpeg|gif|ico|svg|woff2?)$ {\n\
    expires 1y;\n\
    add_header Cache-Control "public, immutable";\n\
  }\n\
}\n' > /etc/nginx/conf.d/default.conf

# Generate env.js at startup for runtime API URL config
COPY <<'EOF' /docker-entrypoint.d/90-env-js.sh
#!/bin/sh
cat > /usr/share/nginx/html/env.js <<ENVJS
window.__env = {
  apiBaseUrl: "${API_BASE_URL:-}"
};
ENVJS
EOF
RUN chmod +x /docker-entrypoint.d/90-env-js.sh

EXPOSE 4200
