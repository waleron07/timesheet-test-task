# Dev-сервер Vite, а не сборка в статику под nginx: для тестового задания это
# на один движущийся элемент меньше, а проксирование /api настраивается в
# vite.config.ts. В проде фронт собирался бы в статику.
FROM node:20-alpine
WORKDIR /app

COPY web/package.json web/package-lock.json* ./
RUN npm install

COPY web/ ./
EXPOSE 5173
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
