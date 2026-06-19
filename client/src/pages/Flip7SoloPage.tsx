import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Flip7Board from '../components/flip7/Flip7Board';
import useAppSelector from '../hooks/useAppSelector';
import flip7Api from '../api/flip7Api';
import type { Flip7GameState } from '../types/flip7';
import styles from './Flip7GamePage.module.css';

/**
 * Single-player Flip 7 — a personal score-chase to the target. Plain
 * request/response against the solo REST endpoints; every response is the
 * authoritative new state (including any pending action-card target choice).
 */
const Flip7SoloPage: React.FC = () => {
  const { gameId } = useParams<{ gameId: string }>();
  const navigate = useNavigate();
  const { user, token } = useAppSelector((s) => s.auth);

  const [state, setState] = useState<Flip7GameState | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!gameId || !token) return;
    flip7Api
      .getSolo(gameId, token)
      .then(setState)
      .catch((err) => setError((err as Error).message));
  }, [gameId, token]);

  const run = useCallback(
    async (fn: () => Promise<Flip7GameState>) => {
      if (!gameId || !token || busy) return;
      setBusy(true);
      setError(null);
      try {
        setState(await fn());
      } catch (err) {
        setError((err as Error).message);
      } finally {
        setBusy(false);
      }
    },
    [gameId, token, busy]
  );

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.orbCyan} />
        <div className={styles.orbPink} />
        <div className={styles.headerArea}>
          <div className={styles.titleBlock}>
            <h1 className={styles.title}>
              Flip <span>7</span>
            </h1>
            <p className={styles.tagline}>Solo — chase {state?.targetScore ?? 200} points</p>
          </div>
          {state && <div className={styles.roundTracker}>Round {state.roundNumber}</div>}
        </div>

        {error && <div className={styles.errorBanner}>{error}</div>}
        {!state && !error && <p className={styles.loadingText}>Dealing…</p>}

        {state && user && (
          <Flip7Board
            state={state}
            myUserId={user.id}
            busy={busy}
            onHit={() => run(() => flip7Api.soloHit(gameId!, token!))}
            onStay={() => run(() => flip7Api.soloStay(gameId!, token!))}
            onChooseTarget={(t) => run(() => flip7Api.soloChooseTarget(gameId!, t, token!))}
            onNextRound={() => run(() => flip7Api.soloNextRound(gameId!, token!))}
            onExit={() => navigate('/flip7')}
          />
        )}
      </div>
    </Layout>
  );
};

export default Flip7SoloPage;
