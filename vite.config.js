import react from "@vitejs/plugin-react";
import { defineConfig, splitVendorChunkPlugin } from "vite";
import { dependencies } from "./package.json";
import { resolve } from 'path';

const vendorDeps = ['react', 'react-dom']

const chunksFromDeps = (deps, vendorDeps) => {
    const chunks = {}
    Object.keys(deps).forEach((key) => {
        if (vendorDeps.includes(key) || key.startsWith('@fluentui')) {
            return
        }
        chunks[key] = [key]
    })
    return chunks
}

const serverPort = 5000
const clientPort = 8082
const previewPort = 8083

const proxy = {
    target: `http://0.0.0.0:${serverPort}/`,
    changeOrigin: false,
    // Proxy X-headers
    xfwd: true,
    autoRewrite: true,
}


const plugins = [
    react(),
];

/** @type {import('vite').UserConfig} */
export default defineConfig({
    plugins: plugins,
    base: "/",
    root: ".",
    clearScreen: false,
    assetsInclude: ['**/*.pdf'],
    publicDir: resolve(__dirname, "./Client/public"),
    build: {
        outDir: resolve(__dirname, "./dist/public"),
        emptyOutDir: true,
        sourcemap: true,
        rollupOptions: {
            output: {
                entryFileNames: 'assets/js/[name].[hash].js',
                chunkFileNames: 'assets/js/[name].[hash].js',
                assetFileNames: 'assets/[ext]/[name].[hash].[ext]'
            }
        },
        minify: "oxc"
    },
    server: {
        host: '0.0.0.0',
        port: clientPort,
        strictPort: true,
        proxy: {
            '/api': proxy,
            "/api/internal/auth/contestSession": proxy,
            "/signin-oidc": proxy,
            "/signin": proxy,
            "/api/internal/auth/signin": proxy,
            "/api/internal/auth/signout": proxy,
        },
        watch: {
            ignored: [
                "**/*.fs" // Don't watch F# files
            ],
        }
    },
    preview: {
        host: '0.0.0.0',
        port: previewPort,
        strictPort: true,
        proxy: {
            '/api': proxy,
            "/api/internal/auth/contestSession": proxy,
            "/signin-oidc": proxy,
            "/signin": proxy,
            "/api/internal/auth/signin": proxy,
            "/api/internal/auth/signout": proxy,
        }
    }
});
