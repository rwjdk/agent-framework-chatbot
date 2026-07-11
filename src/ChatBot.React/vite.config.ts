import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const apiTarget =
  process.env["services__chatbot-api__http__0"] ??
  process.env["services__chatbot-api__https__0"] ??
  "https://localhost:7257";

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          fluent: ["@fluentui/react-components", "@fluentui/react-icons"],
          markdown: ["react-markdown", "remark-gfm"],
        },
      },
    },
  },
  server: {
    host: true,
    proxy: {
      "/api": {
        target: apiTarget,
        secure: false,
      },
    },
  },
});
