import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { visualizer } from 'rollup-plugin-visualizer'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    visualizer({
      filename: 'bundle-stats.html',
      open: false,
      gzipSize: true,
      brotliSize: true,
    }),
  ],
  server: {
    host: '0.0.0.0',
    port: 8585,
    proxy: {
      '/api': {
        target: 'http://localhost:5052',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:5052',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: '../src/Shortboxerr.Api/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          'react-vendor': ['react', 'react-dom', 'react-router-dom'],
          'query': ['@tanstack/react-query'],
          'icons': ['lucide-react'],
        },
      },
    },
  },
})
