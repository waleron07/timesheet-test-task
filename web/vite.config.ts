import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Прокси на API: фронт всегда ходит по относительному /api, поэтому
// один и тот же код работает и локально, и внутри compose — меняется
// только target из переменной окружения.
export default defineConfig({
    plugins: [react()],
    server: {
        port: 5173,
        proxy: {
            "/api": {
                target: process.env.VITE_API_PROXY_TARGET ?? "http://localhost:8080",
                changeOrigin: true
            }
        }
    }
});
