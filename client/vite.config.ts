import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Same-origin reverse proxy for the three backend services. The client calls
// /svc/auth|the-game|flip7/* (see src/api/config.ts) and the dev server
// forwards to the real service, so there are no CORS or mixed-host issues —
// including when the app is opened from another device on the LAN. Targets
// are overridable for docker (e.g. PROXY_AUTH_TARGET=http://auth:8080).
const proxyTargets = {
  auth: process.env.PROXY_AUTH_TARGET ?? 'http://localhost:5002',
  theGame: process.env.PROXY_THEGAME_TARGET ?? 'http://localhost:5001',
  flip7: process.env.PROXY_FLIP7_TARGET ?? 'http://localhost:5003',
};

const proxy = {
  '/svc/auth': {
    target: proxyTargets.auth,
    changeOrigin: true,
    rewrite: (path: string) => path.replace(/^\/svc\/auth/, ''),
  },
  '/svc/the-game': {
    target: proxyTargets.theGame,
    changeOrigin: true,
    ws: true, // /gamehub SignalR websocket
    rewrite: (path: string) => path.replace(/^\/svc\/the-game/, ''),
  },
  '/svc/flip7': {
    target: proxyTargets.flip7,
    changeOrigin: true,
    ws: true, // /flip7hub SignalR websocket
    rewrite: (path: string) => path.replace(/^\/svc\/flip7/, ''),
  },
};

export default defineConfig({
  plugins: [react()],
  envPrefix: 'REACT_APP_',
  server: {
    port: 3000,
    host: '0.0.0.0',
    open: false,
    proxy,
  },
  preview: {
    port: 3000,
    proxy,
  },
  build: {
    outDir: 'build',
  },
});
