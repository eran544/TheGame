import { AUTH_URL, THEGAME_URL, FLIP7_URL } from './config';

async function handleResponse<T>(response: Response): Promise<T> {
  if (response.ok) {
    // 204 No Content – return empty object
    if (response.status === 204) {
      return {} as T;
    }
    return response.json() as Promise<T>;
  }

  let message = `HTTP ${response.status}: ${response.statusText}`;
  try {
    const body = await response.json();
    if (body?.message) {
      message = body.message;
    } else if (body?.error) {
      message = body.error;
    } else if (body?.title) {
      message = body.title;
    } else if (typeof body === 'string') {
      message = body;
    }
  } catch {
    // ignore JSON parse error – use default message
  }

  throw new Error(message);
}

function buildHeaders(token?: string): HeadersInit {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  return headers;
}

export interface ApiClient {
  get<T>(path: string, token?: string): Promise<T>;
  post<T>(path: string, body: unknown, token?: string): Promise<T>;
  delete<T>(path: string, token?: string): Promise<T>;
}

/** Builds a small fetch wrapper bound to one service's base URL. */
export function createApiClient(baseUrl: string): ApiClient {
  return {
    async get<T>(path: string, token?: string): Promise<T> {
      const response = await fetch(`${baseUrl}${path}`, {
        method: 'GET',
        headers: buildHeaders(token),
      });
      return handleResponse<T>(response);
    },

    async post<T>(path: string, body: unknown, token?: string): Promise<T> {
      const response = await fetch(`${baseUrl}${path}`, {
        method: 'POST',
        headers: buildHeaders(token),
        body: JSON.stringify(body),
      });
      return handleResponse<T>(response);
    },

    async delete<T>(path: string, token?: string): Promise<T> {
      const response = await fetch(`${baseUrl}${path}`, {
        method: 'DELETE',
        headers: buildHeaders(token),
      });
      return handleResponse<T>(response);
    },
  };
}

/** Auth service — login/register/logout/me. */
export const authClient = createApiClient(AUTH_URL);

/** Flip 7 service — solo + multiplayer game API. */
export const flip7Client = createApiClient(FLIP7_URL);

/** The Game service. Default export so existing api modules keep working. */
const apiClient = createApiClient(THEGAME_URL);
export default apiClient;

/** Named helpers bound to The Game service (legacy import style). */
export const get = apiClient.get;
export const post = apiClient.post;
export const del = apiClient.delete;
