FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
# Nitro/Vite proxy target for container networking (override in compose)
ARG NUXT_DEV_BACKEND_URL=http://backend:5000
ENV NUXT_DEV_BACKEND_URL=$NUXT_DEV_BACKEND_URL
RUN npm run build

FROM node:20-alpine AS runtime
WORKDIR /app
COPY --from=build /app/.output /app/.output
# Клиентский JS не должен звать localhost:5000 при открытии сайта по LAN-IP; при необходимости задайте при запуске контейнера.
ENV NUXT_PUBLIC_API_BASE_URL=
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
