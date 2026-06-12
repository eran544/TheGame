/**
 * Per-service base URLs. The platform runs three backends:
 *  - Auth service (login/register/logout, issues the platform JWT)
 *  - The Game service (game API + /gamehub)
 *  - Flip 7 service (game API + /flip7hub)
 *
 * Defaults are same-origin /svc/* paths served by the dev server's reverse
 * proxy (see vite.config.ts), which avoids CORS entirely — including LAN
 * access from other devices. Set the REACT_APP_*_URL env vars to call the
 * services on absolute URLs instead. REACT_APP_API_BASE_URL is the legacy
 * single-backend variable, kept as a fallback for The Game.
 */
export const AUTH_URL = import.meta.env.REACT_APP_AUTH_URL ?? '/svc/auth';

export const THEGAME_URL =
  import.meta.env.REACT_APP_THEGAME_URL ??
  import.meta.env.REACT_APP_API_BASE_URL ??
  '/svc/the-game';

export const FLIP7_URL = import.meta.env.REACT_APP_FLIP7_URL ?? '/svc/flip7';
