import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import useAppSelector from '../hooks/useAppSelector';
import flip7Api from '../api/flip7Api';
import styles from './Flip7MenuPage.module.css';

const Flip7MenuPage: React.FC = () => {
  const navigate = useNavigate();
  const { token } = useAppSelector((state) => state.auth);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [joinId, setJoinId] = useState('');

  const startSolo = async () => {
    if (!token) return;
    setBusy(true);
    setError(null);
    try {
      const state = await flip7Api.createSolo(null, token);
      navigate(`/flip7/solo/${state.id}`);
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  };

  const joinGame = () => {
    const id = joinId.trim();
    if (id) navigate(`/flip7/game/${id}`);
  };

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.orbCyan} />
        <div className={styles.orbPink} />
        <div className={styles.titleBlock}>
          <h1 className={styles.title}>
            Flip <span>7</span>
          </h1>
          <p className={styles.tagline}>Press your luck — race to 200 points</p>
        </div>

        <div className={styles.actions}>
          <Button variant="primary" size="lg" fullWidth onClick={startSolo} isLoading={busy}>
            Play Solo
          </Button>
          <Button
            variant="secondary"
            size="lg"
            fullWidth
            onClick={() => navigate('/flip7/setup/vs-ai')}
          >
            Vs AI Players
          </Button>
          <Button
            variant="secondary"
            size="lg"
            fullWidth
            onClick={() => navigate('/flip7/setup/online')}
          >
            Online Game
          </Button>
        </div>

        <div className={styles.joinRow}>
          <input
            className={styles.joinInput}
            placeholder="Paste a game ID to join…"
            value={joinId}
            onChange={(e) => setJoinId(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && joinGame()}
          />
          <Button variant="ghost" size="md" onClick={joinGame} disabled={!joinId.trim()}>
            Join
          </Button>
        </div>

        {error && <div className={styles.error}>{error}</div>}

        <div className={styles.rulesCard}>
          <h2>How it works</h2>
          <div className={styles.bulletList}>
            <div>
              🎴 <strong>Hit or Stay:</strong> flip cards to stack points — but a duplicate number
              means <strong>BUST</strong> (0 for the round).
            </div>
            <div>
              ⭐ <strong>Flip 7:</strong> 7 unique numbers ends the round instantly, +15 bonus.
            </div>
            <div>
              ❄️ <strong>Freeze:</strong> banks the target&rsquo;s points and ends their round.
            </div>
            <div>
              🛡️ <strong>Second Chance:</strong> negates one bust.
            </div>
            <div>
              🔄 <strong>Flip Three:</strong> forces three draws in a row.
            </div>
            <div>
              🏆 First to <strong>200 points</strong> triggers the end — highest total wins.
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
};

export default Flip7MenuPage;
