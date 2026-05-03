import { createAsyncThunk, createSlice, PayloadAction } from '@reduxjs/toolkit';
import * as gameApi from '../../api/gameApi';
import * as chatApi from '../../api/chatApi';
import type {
  ChatMessage,
  ChatSendResult,
  FinalScore,
  GamePhase,
  GameStateDto,
  LastMove,
  PlayerInGame,
  StagedPlay,
} from '../../types/game';

export type { GameStateDto };

export interface GameSliceState {
  sessionId: string | null;
  gamePhase: GamePhase;
  ascendingPiles: [number, number];
  descendingPiles: [number, number];
  drawPileCount: number;
  playedCardsCount: number;
  playerHand: number[];
  minCardsThisTurn: number;
  isExpertMode: boolean;
  currentPlayerId: string | null;
  players: PlayerInGame[];
  spectators: string[];
  selectedCard: number | null;
  stagedPlays: StagedPlay[];
  gameMessages: ChatMessage[];
  chatBlocked: { reason: string; violationCount: number } | null;
  finalScore: FinalScore | null;
  lastMove: LastMove | null;
  canUndo: boolean;
  endReason: string | null;
  status: 'idle' | 'loading' | 'failed';
  error: string | null;
}

const initialState: GameSliceState = {
  sessionId: null,
  gamePhase: 'lobby',
  ascendingPiles: [1, 1],
  descendingPiles: [100, 100],
  drawPileCount: 0,
  playedCardsCount: 0,
  playerHand: [],
  minCardsThisTurn: 2,
  isExpertMode: false,
  currentPlayerId: null,
  players: [],
  spectators: [],
  selectedCard: null,
  stagedPlays: [],
  gameMessages: [],
  chatBlocked: null,
  finalScore: null,
  lastMove: null,
  canUndo: false,
  endReason: null,
  status: 'idle',
  error: null,
};

function applyGameState(state: GameSliceState, dto: GameStateDto) {
  state.sessionId = dto.sessionId;
  state.gamePhase = dto.gamePhase;
  state.ascendingPiles = [dto.piles.ascending1, dto.piles.ascending2];
  state.descendingPiles = [dto.piles.descending1, dto.piles.descending2];
  state.drawPileCount = dto.drawPileCount;
  state.playedCardsCount = dto.playedCardsCount;
  state.playerHand = dto.hand;
  state.minCardsThisTurn = dto.minCardsThisTurn;
  state.isExpertMode = dto.isExpertMode;
  state.finalScore = dto.finalScore;
  state.lastMove = dto.lastMove ?? null;
  state.canUndo = dto.canUndo;
  state.currentPlayerId = dto.currentPlayerId ?? null;
  state.players = dto.players ?? [];
  state.selectedCard = null;
  state.stagedPlays = [];
}

// ---------- thunks ----------

export const startGameAsync = createAsyncThunk(
  'game/start',
  async ({ isExpertMode, token }: { isExpertMode: boolean; token: string }, { rejectWithValue }) => {
    try {
      return await gameApi.startGame(isExpertMode, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const playTurnAsync = createAsyncThunk(
  'game/playTurn',
  async (
    { sessionId, plays, token }: { sessionId: string; plays: StagedPlay[]; token: string },
    { rejectWithValue }
  ) => {
    try {
      return await gameApi.playTurn(
        sessionId,
        plays.map((p) => ({ card: p.card, slot: p.pileSlot })),
        token
      );
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const undoMoveAsync = createAsyncThunk(
  'game/undo',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      return await gameApi.undoMove(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const abandonGameAsync = createAsyncThunk(
  'game/abandon',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      await gameApi.abandonGame(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

// Loads an existing session by ID — used when joining a multiplayer game in progress.
export const loadGameAsync = createAsyncThunk(
  'game/load',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      return await gameApi.getGameState(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const sendChatMessageAsync = createAsyncThunk<
  ChatSendResult,
  { sessionId: string; message: string; token: string }
>(
  'game/sendChatMessage',
  async ({ sessionId, message, token }, { rejectWithValue }) => {
    try {
      return await chatApi.sendMessage(sessionId, message, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

export const loadChatHistoryAsync = createAsyncThunk(
  'game/loadChatHistory',
  async ({ sessionId, token }: { sessionId: string; token: string }, { rejectWithValue }) => {
    try {
      return await chatApi.getChatHistory(sessionId, token);
    } catch (e: unknown) {
      return rejectWithValue((e as Error).message);
    }
  }
);

// ---------- slice ----------

const gameSlice = createSlice({
  name: 'game',
  initialState,
  reducers: {
    selectCard(state, action: PayloadAction<number>) {
      state.selectedCard = state.selectedCard === action.payload ? null : action.payload;
    },

    stagePlay(state, action: PayloadAction<StagedPlay>) {
      const { card, pileSlot } = action.payload;
      if (state.selectedCard !== card) return;
      state.stagedPlays.push({ card, pileSlot });
      state.selectedCard = null;
    },

    unstagePaly(state, action: PayloadAction<number>) {
      state.stagedPlays.splice(action.payload, 1);
    },

    clearStagedPlays(state) {
      state.stagedPlays = [];
      state.selectedCard = null;
    },

    applyGameStateFromHub(state, action: PayloadAction<GameStateDto>) {
      if (state.sessionId !== action.payload.sessionId) return;
      // Never overwrite the hand from a hub broadcast — the DTO carries the
      // sending player's cards, not ours. The hand is only updated via HTTP
      // responses (startGame, playTurn, undo, loadGame).
      const preservedHand = state.playerHand;
      applyGameState(state, action.payload);
      state.playerHand = preservedHand;
    },

    gameEndedFromHub(state, action: PayloadAction<{ reason: string }>) {
      if (state.gamePhase === 'ended') return;
      state.gamePhase = 'ended';
      state.endReason = action.payload.reason;
      state.selectedCard = null;
      state.stagedPlays = [];
    },

    addChatMessage(state, action: PayloadAction<ChatMessage>) {
      if (!state.gameMessages.some((m) => m.id === action.payload.id)) {
        state.gameMessages.push(action.payload);
      }
    },

    clearChatBlocked(state) {
      state.chatBlocked = null;
    },

    clearGame() {
      return { ...initialState };
    },

    clearGameError(state) {
      state.error = null;
    },
  },

  extraReducers: (builder) => {
    // startGame
    builder
      .addCase(startGameAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(startGameAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyGameState(state, action.payload);
      })
      .addCase(startGameAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload as string;
      });

    // playTurn
    builder
      .addCase(playTurnAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(playTurnAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyGameState(state, action.payload.state);
      })
      .addCase(playTurnAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload as string;
      });

    // undoMove
    builder
      .addCase(undoMoveAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(undoMoveAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyGameState(state, action.payload);
      })
      .addCase(undoMoveAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload as string;
      });

    // loadGame (multiplayer join)
    builder
      .addCase(loadGameAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(loadGameAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        applyGameState(state, action.payload);
      })
      .addCase(loadGameAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload as string;
      });

    // abandonGame
    builder
      .addCase(abandonGameAsync.pending, (state) => {
        state.status = 'loading';
      })
      .addCase(abandonGameAsync.fulfilled, (state) => {
        state.status = 'idle';
        state.gamePhase = 'ended';
      })
      .addCase(abandonGameAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload as string;
      });

    // sendChatMessage
    builder
      .addCase(sendChatMessageAsync.fulfilled, (state, action) => {
        const result = action.payload;
        if (result.isBlocked) {
          state.chatBlocked = {
            reason: result.blockReason ?? 'Message blocked',
            violationCount: result.violationCount,
          };
        } else {
          state.chatBlocked = null;
          if (result.message) {
            if (!state.gameMessages.some((m) => m.id === result.message!.id)) {
              state.gameMessages.push(result.message);
            }
          }
        }
      });

    // loadChatHistory
    builder
      .addCase(loadChatHistoryAsync.fulfilled, (state, action) => {
        state.gameMessages = action.payload;
      });
  },
});

export const {
  selectCard,
  stagePlay,
  unstagePaly,
  clearStagedPlays,
  applyGameStateFromHub,
  gameEndedFromHub,
  addChatMessage,
  clearChatBlocked,
  clearGame,
  clearGameError,
} = gameSlice.actions;

export default gameSlice.reducer;
