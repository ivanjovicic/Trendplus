import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
    build: {
        sourcemap: true,
        minify: 'esbuild',  // Changed from 'terser' to 'esbuild' for proper UTF-8 support
    },
    plugins: [react()],
    server: {
        port: 5173,
        strictPort: true,
        proxy: {
            // Proxy all /api requests to backend
            "/api": {
                target: "http://localhost:8080",
                changeOrigin: true,
            },
        },
    },
});
