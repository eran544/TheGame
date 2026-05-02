import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';
import useAppDispatch from '../hooks/useAppDispatch';
import useAppSelector from '../hooks/useAppSelector';
import { createMultiplayerGameAsync, joinGameAsync } from '../store/slices/lobbySlice';
import styles from './MultiplayerPage.module.css';

const MultiplayerPage: React.FC = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { token } = useAppSelector((s) => s.auth);
  const { status, error } = useAppSelector((s) => s.lobby);

  const [maxPlayers, setMaxPlayers] = useState(4);
  const [isExpertMode, setIsExpertMode] = useState(false);
  const [joinId, setJoinId] = useState('');

  const handleCreate = async () => {
    if (!token) return;
    const result = await dispatch(createMultiplayerGameAsync({ maxPlayers, isExpertMode, token }));
    if (createMultiplayerGameAsync.fulfilled.match(result)) {
      navigate(`/lobby/${result.payload.sessionId}`);
    }
  };

  const handleJoin = async () => {
    if (!token || !joinId.trim()) return;
    const result = await dispatch(joinGameAsync({ sessionId: joinId.trim(), token }));
    if (joinGameAsync.fulfilled.match(result)) {
      navigate(`/lobby/${result.payload.sessionId}`);
    }
  };

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <h2 className={styles.title}>Multiplayer</h2>

        {error && <p className={styles.error}>{error}</p>}

        <section className={styles.section}>
          <h3 className={styles.sectionTitle}>Create a Game</h3>

          <div className={styles.field}>
            <label className={styles.label}>Players</label>
            <div className={styles.playerBtns}>
              {[2, 3, 4, 5].map((n) => (
                <button
                  key={n}
                  className={[styles.playerBtn, maxPlayers === n ? styles.playerBtnActive : ''].join(' ')}
                  onClick={() => setMaxPlayers(n)}
                >
                  {n}
                </button>
              ))}
            </div>
          </div>

          <label className={styles.toggleRow}>
            <input
              type="checkbox"
              checked={isExpertMode}
              onChange={(e) => setIsExpertMode(e.target.checked)}
              className={styles.checkbox}
            />
            <span className={styles.toggleLabel}>Expert mode</span>
            <span className={styles.toggleHint}>(3 cards min per turn)</span>
          </label>

          <Button
            variant="primary"
            fullWidth
            onClick={handleCreate}
            disabled={status === 'loading'}
          >
            {status === 'loading' ? 'Creating…' : 'Create Game'}
          </Button>
        </section>

        <div className={styles.divider}>or</div>

        <section className={styles.section}>
          <h3 className={styles.sectionTitle}>Join a Game</h3>
          <Input
            placeholder="Paste session ID"
            value={joinId}
            onChange={(e) => setJoinId(e.target.value)}
          />
          <Button
            variant="secondary"
            fullWidth
            onClick={handleJoin}
            disabled={status === 'loading' || !joinId.trim()}
          >
            {status === 'loading' ? 'Joining…' : 'Join Game'}
          </Button>
        </section>

        <button className={styles.backLink} onClick={() => navigate('/')}>
          ← Back to Menu
        </button>
      </div>
    </Layout>
  );
};

export default MultiplayerPage;
