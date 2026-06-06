import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import useAppSelector from '../hooks/useAppSelector';
import useAppDispatch from '../hooks/useAppDispatch';
import { fetchStatisticsAsync } from '../store/slices/statsSlice';
import { GameHistoryItem } from '../types/game';
import styles from './DashboardPage.module.css';

const RATING_META: Record<GameHistoryItem['rating'], { emoji: string; label: string; cls: string }> = {
  Perfect:  { emoji: '🏆', label: 'Perfect',   cls: styles.ratingPerfect },
  Excellent:{ emoji: '⭐', label: 'Excellent',  cls: styles.ratingExcellent },
  TryAgain: { emoji: '🎴', label: 'Try Again',  cls: styles.ratingTryAgain },
};

function formatDuration(minutes: number | null): string {
  if (minutes === null || minutes < 0) return '—';
  if (minutes < 60) return `${minutes}m`;
  return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
  });
}

interface StatCardProps {
  label: string;
  value: string | number;
  accentColor?: string;
}

const StatCard: React.FC<StatCardProps> = ({ label, value, accentColor }) => (
  <div className={styles.statCard}>
    <div className={styles.statValue} style={accentColor ? { color: accentColor } : undefined}>
      {value}
    </div>
    <div className={styles.statLabel}>{label}</div>
  </div>
);

const DashboardPage: React.FC = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { token } = useAppSelector((state) => state.auth);
  const { statistics: stats, history, status, error } = useAppSelector((state) => state.stats);

  useEffect(() => {
    if (token) dispatch(fetchStatisticsAsync(token));
  }, [token, dispatch]);

  const winRate =
    stats && stats.totalGames > 0
      ? Math.round((stats.perfectGames / stats.totalGames) * 100)
      : null;

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.header}>
          <h2 className={styles.pageTitle}>Dashboard</h2>
          <Button variant="ghost" size="sm" onClick={() => navigate('/the-game')}>
            ← Back
          </Button>
        </div>

        {status === 'loading' && <p className={styles.loading}>Loading…</p>}
        {status === 'failed' && <p className={styles.errorBanner}>{error}</p>}

        <div className={styles.statsGrid}>
          <StatCard label="Games Played" value={stats?.totalGames ?? 0} />
          <StatCard
            label="Perfect Games"
            value={stats?.perfectGames ?? 0}
            accentColor="#f39c12"
          />
          <StatCard
            label="Win Rate"
            value={winRate !== null ? `${winRate}%` : '—'}
          />
          <StatCard
            label="Best Score"
            value={
              stats?.bestScore !== null && stats?.bestScore !== undefined
                ? `${stats.bestScore} left`
                : '—'
            }
            accentColor="var(--color-pile-asc)"
          />
          <StatCard
            label="Avg Cards Left"
            value={
              stats && stats.totalGames > 0
                ? stats.averageRemainingCards.toFixed(1)
                : '—'
            }
          />
          <StatCard
            label="Play Time"
            value={formatDuration(stats?.totalPlayTimeMinutes ?? null)}
          />
        </div>

        <div className={styles.historySection}>
          <h3 className={styles.sectionTitle}>Recent Games</h3>

          {history.length === 0 && status !== 'loading' ? (
            <div className={styles.emptyState}>
              <p>No games played yet — start your first game!</p>
              <Button variant="primary" onClick={() => navigate('/the-game/game/new')}>
                Play Now
              </Button>
            </div>
          ) : (
            <div className={styles.historyList}>
              {history.map((game) => {
                const meta = RATING_META[game.rating] ?? RATING_META.TryAgain;
                return (
                  <div key={game.sessionId} className={styles.historyItem}>
                    <span className={styles.historyDate}>{formatDate(game.playedAt)}</span>
                    <span className={[styles.ratingBadge, meta.cls].join(' ')}>
                      {meta.emoji} {meta.label}
                    </span>
                    <span className={styles.historyCards}>
                      {game.cardsRemaining === 0
                        ? 'All played'
                        : `${game.cardsRemaining} left`}
                    </span>
                    <span className={styles.historyDuration}>
                      {formatDuration(game.durationMinutes)}
                    </span>
                    {game.isExpertMode && (
                      <span className={styles.expertBadge}>Expert</span>
                    )}
                    {game.endReason === 'abandoned' && (
                      <span className={styles.abandonedBadge}>Abandoned</span>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </Layout>
  );
};

export default DashboardPage;
