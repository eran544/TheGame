import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import type {
  ChatMessage,
  GamePhase,
  GameResult,
  Player,
  Spectator,
  ValidMove,
} from '../../types/game';

export interface GameSliceState {
  sessionId: string | null;
  gamePhase: GamePhase;
  ascendingPiles: [number, number];
  descendingPiles: [number, number];
  drawPileCount: number;
  playedCardsCount: number;
  playerHand: number[];
  currentPlayer: string;
  players: Player[];
  spectators: Spectator[];
  selectedCards: number[];
  validMoves: ValidMove[];
  gameMessages: ChatMessage[];
  score: GameResult | null;
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
  currentPlayer: '',
  players: [],
  spectators: [],
  selectedCards: [],
  validMoves: [],
  gameMessages: [],
  score: null,
  status: 'idle',
  error: null,
};

const gameSlice = createSlice({
  name: 'game',
  initialState,
  reducers: {
    selectCard(state, action: PayloadAction<number>) {
      const cardValue = action.payload;
      const idx = state.selectedCards.indexOf(cardValue);
      if (idx === -1) {
        state.selectedCards.push(cardValue);
      } else {
        state.selectedCards.splice(idx, 1);
      }
    },

    clearSelectedCards(state) {
      state.selectedCards = [];
    },

    setGameState(state, action: PayloadAction<Partial<GameSliceState>>) {
      return { ...state, ...action.payload };
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
});

export const {
  selectCard,
  clearSelectedCards,
  setGameState,
  addChatMessage,
  clearGame,
  clearGameError,
} = gameSlice.actions;

export default gameSlice.reducer;
