import { createAsyncThunk, createSlice, PayloadAction } from '@reduxjs/toolkit';
import { authClient } from '../../api/apiClient';
import type { AuthResult, LoginRequest, RegisterRequest, User } from '../../types/user';

// ---------- helpers ----------

function isTokenExpired(token: string): boolean {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return true;
    // Pad base64url to standard base64
    const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = payload + '=='.slice(0, (4 - (payload.length % 4)) % 4);
    const decoded = JSON.parse(atob(padded));
    if (!decoded.exp) return false;
    return Date.now() / 1000 > decoded.exp;
  } catch {
    return true;
  }
}

function loadFromStorage(): { token: string | null; user: User | null } {
  try {
    const token = localStorage.getItem('auth_token');
    const userRaw = localStorage.getItem('auth_user');
    if (!token || !userRaw) return { token: null, user: null };
    if (isTokenExpired(token)) {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth_user');
      return { token: null, user: null };
    }
    return { token, user: JSON.parse(userRaw) as User };
  } catch {
    return { token: null, user: null };
  }
}

function saveToStorage(token: string, user: User): void {
  localStorage.setItem('auth_token', token);
  localStorage.setItem('auth_user', JSON.stringify(user));
}

function clearStorage(): void {
  localStorage.removeItem('auth_token');
  localStorage.removeItem('auth_user');
}

// ---------- state ----------

export interface AuthState {
  user: User | null;
  token: string | null;
  status: 'idle' | 'loading' | 'succeeded' | 'failed';
  error: string | null;
}

const persisted = loadFromStorage();

const initialState: AuthState = {
  user: persisted.user,
  token: persisted.token,
  status: 'idle',
  error: null,
};

// ---------- thunks ----------

export const loginAsync = createAsyncThunk<
  AuthResult,
  LoginRequest,
  { rejectValue: string }
>('auth/login', async (credentials, { rejectWithValue }) => {
  try {
    const result = await authClient.post<AuthResult>(
      '/api/auth/login',
      credentials
    );
    return result;
  } catch (err) {
    return rejectWithValue((err as Error).message);
  }
});

export const registerAsync = createAsyncThunk<
  AuthResult,
  RegisterRequest,
  { rejectValue: string }
>('auth/register', async (request, { rejectWithValue }) => {
  try {
    const result = await authClient.post<AuthResult>(
      '/api/auth/register',
      request
    );
    return result;
  } catch (err) {
    return rejectWithValue((err as Error).message);
  }
});

export const logoutAsync = createAsyncThunk<
  void,
  void,
  { state: { auth: AuthState }; rejectValue: string }
>('auth/logout', async (_, { getState, rejectWithValue }) => {
  try {
    const token = getState().auth.token ?? undefined;
    await authClient.post('/api/auth/logout', {}, token);
  } catch (err) {
    // Even if the server call fails we still want to clear local state,
    // so we resolve rather than reject.
    return rejectWithValue((err as Error).message);
  }
});

// ---------- slice ----------

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    clearAuthError(state) {
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    // ----- login -----
    builder
      .addCase(loginAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(loginAsync.fulfilled, (state, action) => {
        state.status = 'succeeded';
        state.token = action.payload.token;
        state.user = action.payload.user;
        saveToStorage(action.payload.token, action.payload.user);
      })
      .addCase(loginAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload ?? 'Login failed';
        clearStorage();
      });

    // ----- register -----
    builder
      .addCase(registerAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(registerAsync.fulfilled, (state, action) => {
        state.status = 'succeeded';
        state.token = action.payload.token;
        state.user = action.payload.user;
        saveToStorage(action.payload.token, action.payload.user);
      })
      .addCase(registerAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload ?? 'Registration failed';
        clearStorage();
      });

    // ----- logout -----
    builder
      .addCase(logoutAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(logoutAsync.fulfilled, (state) => {
        state.status = 'idle';
        state.token = null;
        state.user = null;
        clearStorage();
      })
      .addCase(logoutAsync.rejected, (state) => {
        // Clear state regardless of server error
        state.status = 'idle';
        state.token = null;
        state.user = null;
        clearStorage();
      });
  },
});

export const { clearAuthError } = authSlice.actions;
export default authSlice.reducer;
