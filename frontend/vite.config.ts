import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { readFileSync } from "node:fs";
const pkg = JSON.parse(readFileSync(new URL("./package.json", import.meta.url), "utf-8"));
export default defineConfig({
  define: { __APP_VERSION__: JSON.stringify(pkg.version) },
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      "/api": "http://127.0.0.1:5080",
      "/hubs": { target: "http://127.0.0.1:5080", ws: true },
    },
  },
  build: { chunkSizeWarningLimit: 650 },
});
