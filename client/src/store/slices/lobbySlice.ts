import { createAsyncThunk, createSlice, PayloadAction } from '@reduxjs/toolkit';
import * as gameApi from '../../api/gameApi';
import type { GamePhase, LobbyPlayer, LobbyStateDto } from '../../types/game';

export type { LobbyStateDto };

export interface LobbySliceState {
  sessionId: string | null;
  gamePhase: GamePhase;
  players: LobbyPlayer[];
  maxPlayers: number;
  isExpertMode: boolean;
  canStart: boolean;
  createdBy: string | null;
  status: 'idle' | 'loading' | 'failed';
  error: string | null;
}

const initialState: LobbySliceState = {
  sessionId: null,
  gamePhase: 'lobby',
  players: [],
  maxPlayers: 4,
  isExpertMode: false,
  canStart: false,
  createdBy: null,
  status: 'idle',
  error: null,
};

function applyLobbyState(state: LobbySliceState, dto: LobbyStateDto) {
  state.sessionId = dto.sessionId;
  state.gamePhase = dto.gamePhase;
  state.players = dto.players;
  state.maxPlayers = dto.maxPlayers;
  state.isExpertMode = dto.isExpertMode;
  state.canStart = dto.canStart;
  state.createdBy = dto.createdBy;
}

// ---------- thunks ----------

export const createMultiplayerGameAsync = createAsyncThunk(
  'lobby/create',
  async (
    { maxPlayers, isExpertMode, token }: { maxPlayers: number; isExpertMode: boolean; token: string },
    { rejectWithValue }
  ) => {
    try {
      return await gameApi.createMultiplayerGame(maxPlayers, isExpertMode, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const joinGameAsync = createAsyncThunk(
  'lobby/join',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      return await gameApi.joinGame(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const leaveGameAsync = createAsyncThunk(
  'lobby/leave',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      await gameApi.leaveGame(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const fetchLobbyStateAsync = createAsyncThunk(
  'lobby/fetch',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      return await gameApi.getLobbyState(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const addAIPlayerAsync = createAsyncThunk(
  'lobby/addAI',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      return await gameApi.addAIPlayer(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const removeAIPlayerAsync = createAsyncThunk(
  'lobby/removeAI',
  async (
    { sessionId, aiUserId, token }: { sessionId: string; aiUserId: string; token: string },
    { rejectWithValue }
  ) => {
    try {
      return await gameApi.removeAIPlayer(sessionId, aiUserId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

// ---------- slice ----------

const lobbySlice = createSlice({
  name: 'lobby',
  initialState,
  reducers: {
    applyLobbyStateFromHub(state, action: PayloadAction<LobbyStateDto>) {
      if (state.sessionId === action.payload.sessionId) {
        applyLobbyState(state, action.payload);
      }
    },
    clearLobby() {
      return { ...initialState };
    },
    clearLobbyError(state) {
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    const pending = (state: LobbySliceState) => {
      state.status = 'loading';
      state.error = null;
    };
    const failed = (state: LobbySliceState, action: { payload: unknown }) => {
      state.status = 'failed';
      state.error = action.payload as string;
    };

    builder
      .addCase(createMultiplayerGameAsync.pending, pending)
      .addCase(createMultiplayerGameAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyLobbyState(state, action.payload);
      })
      .addCase(createMultiplayerGameAsync.rejected, failed);

    builder
      .addCase(joinGameAsync.pending, pending)
      .addCase(joinGameAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyLobbyState(state, action.payload);
      })
      .addCase(joinGameAsync.rejected, failed);

    builder
      .addCase(leaveGameAsync.pending, pending)
      .addCase(leaveGameAsync.fulfilled, (state) => {
        state.status = 'idle';
        return { ...initialState };
      })
      .addCase(leaveGameAsync.rejected, failed);

    builder
      .addCase(fetchLobbyStateAsync.pending, pending)
      .addCase(fetchLobbyStateAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyLobbyState(state, action.payload);
      })
      .addCase(fetchLobbyStateAsync.rejected, failed);

    builder
      .addCase(addAIPlayerAsync.pending, pending)
      .addCase(addAIPlayerAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyLobbyState(state, action.payload);
      })
      .addCase(addAIPlayerAsync.rejected, failed);

    builder
      .addCase(removeAIPlayerAsync.pending, pending)
      .addCase(removeAIPlayerAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyLobbyState(state, action.payload);
      })
      .addCase(removeAIPlayerAsync.rejected, failed);
  },
});

export const { applyLobbyStateFromHub, clearLobby, clearLobbyError } = lobbySlice.actions;
export default lobbySlice.reducer;
