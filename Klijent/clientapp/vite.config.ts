import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";

const devHost = "127.0.0.1";
const devPort = 5174;
const clientRoot = fileURLToPath(new URL(".", import.meta.url));

export default defineConfig({
    root: clientRoot,
    build: {
        sourcemap: true,
        minify: "esbuild", // Changed from 'terser' to 'esbuild' for proper UTF-8 support
        rollupOptions: {
            output: {
                manualChunks(id) {
                    if (id.includes("node_modules/recharts")) {
                        return "recharts";
                    }
                },
            },
        },
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
            // Proxy lightweight status probes for backend status/failover checks
            "/health": {
                target: "http://127.0.0.1:8080",
                changeOrigin: true,
            },
            "/ready": {
                target: "http://127.0.0.1:8080",
                changeOrigin: true,
            },
        },
    },
});
