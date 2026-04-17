FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-alpine AS runtime
WORKDIR /app
COPY --from=build /app/.output /app/.output
# Клиентский JS не должен звать localhost:5000 при открытии сайта по LAN-IP; при необходимости задайте при запуске контейнера.
ENV NUXT_PUBLIC_API_BASE_URL=
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
