import { createAsyncThunk, createSlice, PayloadAction } from '@reduxjs/toolkit';
import * as gameApi from '../../api/gameApi';
import type {
  ChatMessage,
  FinalScore,
  GamePhase,
  GameStateDto,
  Player,
  Spectator,
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
  currentPlayer: string;
  players: Player[];
  spectators: Spectator[];
  selectedCard: number | null;
  stagedPlays: StagedPlay[];
  gameMessages: ChatMessage[];
  finalScore: FinalScore | null;
  canUndo: boolean;
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
  currentPlayer: '',

  players: [],
  spectators: [],
  selectedCard: null,
  stagedPlays: [],
  gameMessages: [],
  finalScore: null,
  canUndo: false,
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
  state.canUndo = dto.canUndo;
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
      // Only apply if the session matches and the game is still in progress
      if (state.sessionId === action.payload.sessionId) {
        applyGameState(state, action.payload);
      }
    },

    addChatMessage(state, action: PayloadAction<ChatMessage>) {
      state.gameMessages.push(action.payload);
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
  },
});

export const {
  selectCard,
  stagePlay,
  unstagePaly,
  clearStagedPlays,
  applyGameStateFromHub,
  addChatMessage,
  clearGame,
  clearGameError,
} = gameSlice.actions;

export default gameSlice.reducer;
