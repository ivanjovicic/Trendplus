import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd());

    return {
        build: {
            sourcemap: true,
        },
        plugins: [react()],
        server: {
            port: 5173,
            strictPort: true,
            proxy:
                mode === "development"
                    ? {
                        "/api": { target: env.VITE_API_BASE_URL, changeOrigin: true },
                        "/artikli": { target: env.VITE_API_BASE_URL, changeOrigin: true },
                        "/tipovi-obuce": { target: env.VITE_API_BASE_URL, changeOrigin: true },
                        "/dobavljaci": { target: env.VITE_API_BASE_URL, changeOrigin: true },
                    }
                    : undefined,
        },
    };
});
