import path from "path";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig, loadEnv } from "vite";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");

  return {
    plugins: [
      tanstackRouter({
        target: "react",
        autoCodeSplitting: true,
      }),
      tailwindcss(),
      react(),
    ],
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "./src"),
      },
    },
    build: {
      sourceMap: true,
      outDir: "build",
      emptyOutDir: true,
      assetsDir: "assets",
      rollupOptions: {
        output: {
          manualChunks: undefined,
          entryFileNames: `assets/index.js`,
          chunkFileNames: `assets/[name]-chunk.js`,
          assetFileNames: `assets/[name].[ext]`,
          format: "es",
        },
      },
    },
    server: {
      port: 5173,
      host: true,
      proxy: {
        "/api": {
          target: env.VITE_API_URL || "https://localhost:9020",
          changeOrigin: true,
          secure: false,
          ws: true,
        },
        "/embeddinghub": {
          target: env.VITE_SIGNALR_HUB_URL || "https://localhost:9020",
          changeOrigin: true,
          secure: false,
          ws: true,
        },
      },
    },
  };
});
