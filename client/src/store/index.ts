import { configureStore } from '@reduxjs/toolkit';
import authReducer from './slices/authSlice';
import gameReducer from './slices/gameSlice';
import lobbyReducer from './slices/lobbySlice';
import statsReducer from './slices/statsSlice';

const store = configureStore({
  reducer: {
    auth: authReducer,
    game: gameReducer,
    lobby: lobbyReducer,
    stats: statsReducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;

export default store;
