import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: 'src/relewise-dashboard.element.ts',
      formats: ["es"],
    },
    outDir: "../wwwroot/App_Plugins/relewise-dashboard",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
