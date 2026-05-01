import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as statsApi from '../../api/statsApi';
import { PlayerStatistics } from '../../types/user';
import { GameHistoryItem } from '../../types/game';

interface StatsState {
  statistics: PlayerStatistics | null;
  history: GameHistoryItem[];
  status: 'idle' | 'loading' | 'failed';
  error: string | null;
}

const initialState: StatsState = {
  statistics: null,
  history: [],
  status: 'idle',
  error: null,
};

export const fetchStatisticsAsync = createAsyncThunk<
  { statistics: PlayerStatistics | null; history: GameHistoryItem[] },
  string,
  { rejectValue: string }
>('stats/fetch', async (token, { rejectWithValue }) => {
  try {
    const [statistics, history] = await Promise.all([
      statsApi.getMyStatistics(token),
      statsApi.getGameHistory(token),
    ]);
    return { statistics, history };
  } catch (e: unknown) {
    return rejectWithValue((e as Error).message);
  }
});

const statsSlice = createSlice({
  name: 'stats',
  initialState,
  reducers: {
    clearStats: () => initialState,
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchStatisticsAsync.pending, (state) => {
        state.status = 'loading';
        state.error = null;
      })
      .addCase(fetchStatisticsAsync.fulfilled, (state, action) => {
        state.status = 'idle';
        state.statistics = action.payload.statistics;
        state.history = action.payload.history;
      })
      .addCase(fetchStatisticsAsync.rejected, (state, action) => {
        state.status = 'failed';
        state.error = action.payload ?? 'Failed to load statistics';
      });
  },
});

export const { clearStats } = statsSlice.actions;
export default statsSlice.reducer;
