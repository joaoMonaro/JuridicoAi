FROM node:24-bookworm-slim AS build
WORKDIR /source
COPY package.json package-lock.json ./
RUN npm ci
COPY . ./
ARG API_URL
ENV VITE_API_URL=$API_URL
RUN npm run build

FROM nginx:stable-alpine
COPY --from=build /source/dist/ /usr/share/nginx/html/
COPY .rehearsal/nginx.conf /etc/nginx/conf.d/default.conf
