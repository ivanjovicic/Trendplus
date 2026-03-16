import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const devHost = "127.0.0.1";
const devPort = 5174;

export default defineConfig({
    build: {
        sourcemap: true,
        minify: "esbuild", // Changed from 'terser' to 'esbuild' for proper UTF-8 support
    },
    plugins: [react()],
    server: {
        host: devHost,
        port: devPort,
        strictPort: true,
        hmr: {
            host: devHost,
            port: devPort,
            clientPort: devPort,
            protocol: "ws",
        },
        proxy: {
            // Proxy all /api requests to backend (use IPv4 to avoid ::1/IPv6 bind issues)
            "/api": {
                target: "http://127.0.0.1:8080",
                changeOrigin: true,
            },
            // Proxy /health for backend status check
            "/health": {
                target: "http://127.0.0.1:8080",
                changeOrigin: true,
            },
        },
    },
});
