/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Per-service URLs; default to the /svc/* dev-server proxy when unset. */
  readonly REACT_APP_AUTH_URL?: string;
  readonly REACT_APP_THEGAME_URL?: string;
  readonly REACT_APP_FLIP7_URL?: string;
  /** Legacy single-backend URL (fallback for The Game). */
  readonly REACT_APP_API_BASE_URL?: string;
  readonly REACT_APP_SIGNALR_HUB_URL?: string;
  readonly REACT_APP_ENVIRONMENT?: string;
  readonly REACT_APP_ENABLE_DEBUG_LOGGING?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
