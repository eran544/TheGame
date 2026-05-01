const baseUrl =
  import.meta.env.REACT_APP_API_BASE_URL ?? 'http://localhost:5001';

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

export async function get<T>(path: string, token?: string): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'GET',
    headers: buildHeaders(token),
  });
  return handleResponse<T>(response);
}

export async function post<T>(
  path: string,
  body: unknown,
  token?: string
): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    method: 'POST',
    headers: buildHeaders(token),
    body: JSON.stringify(body),
  });
  return handleResponse<T>(response);
}

const apiClient = { get, post };
export default apiClient;
