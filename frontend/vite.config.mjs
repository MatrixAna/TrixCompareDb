import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [vue()]
  ,
  server: {
    // Proxy /api calls to the backend to avoid CORS and ensure correct JSON responses
    proxy: {
      '/api': {
        target: process.env.VITE_API_BASE_URL,
        changeOrigin: true,
            secure: false
      }
    }
  }
})
