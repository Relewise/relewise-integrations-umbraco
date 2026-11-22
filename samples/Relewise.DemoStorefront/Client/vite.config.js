import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
    plugins: [vue()],
    define: { 'process.env.NODE_ENV': JSON.stringify('production') },
    test: { environment: 'happy-dom', include: ['src/**/*.test.js'] },
    build: {
        outDir: '../wwwroot/storefront',
        emptyOutDir: true,
        lib: { entry: 'src/main.js', formats: ['es'], fileName: () => 'listing.js' }
    }
});
