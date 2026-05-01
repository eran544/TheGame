/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly REACT_APP_API_BASE_URL: string;
  readonly REACT_APP_SIGNALR_HUB_URL: string;
  readonly REACT_APP_ENVIRONMENT: string;
  readonly REACT_APP_ENABLE_DEBUG_LOGGING: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
