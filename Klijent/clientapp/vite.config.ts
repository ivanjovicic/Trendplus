import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
// https://vite.dev/config/
export default defineConfig({
    build: {
        sourcemap: true,
    },
    plugins: [react()],
    server: {
        port: 5173,     // fiksni port
        proxy: {
            // svi pozivi ka backendu se prosleđuju backendu
            "/api": { target: "http://localhost:5230", changeOrigin: true },
            "/artikli": { target: "http://localhost:5230", changeOrigin: true },
            "/tipovi-obuce": { target: "http://localhost:5230", changeOrigin: true },
            "/dobavljaci": { target: "http://localhost:5230", changeOrigin: true }
        },
        strictPort: true // ako je zauzet, server neće automatski probati drugi
    }
});