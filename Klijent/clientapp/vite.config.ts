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
            "/api": {
                target: "http://localhost:28136",
                changeOrigin: true,
            },
            "/artikli": {
                target: "http://localhost:28136",
                changeOrigin: true,
            },
            "/tipovi-obuce": {
                target: "http://localhost:28136",
                changeOrigin: true,
            },
            "/dobavljaci": {
                target: "http://localhost:28136",
                changeOrigin: true,
            },
            "/errors": {
                target: "http://localhost:28136",
                changeOrigin: true,
            },
            "/health": {
                target: "http://localhost:28136",
                changeOrigin: true,
            },
        },
    },
});
