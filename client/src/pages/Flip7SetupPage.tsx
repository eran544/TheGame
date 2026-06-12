import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import useAppSelector from '../hooks/useAppSelector';
import flip7Api from '../api/flip7Api';
import type { Flip7AiDifficulty, Flip7AiSpec, Flip7AiStyle } from '../types/flip7';
import styles from './Flip7SetupPage.module.css';

const STYLES: Flip7AiStyle[] = ['safe', 'balanced', 'risky'];
const DIFFICULTIES: Flip7AiDifficulty[] = ['easy', 'medium', 'hard'];
const AI_NAMES = ['Nova', 'Vega', 'Quark', 'Pixel', 'Bolt'];
const MAX_AI = 5;

const defaultAi = (index: number): Flip7AiSpec => ({
  username: AI_NAMES[index % AI_NAMES.length],
  style: 'balanced',
  difficulty: 'medium',
});

/**
 * Pre-game setup for vs-AI and online games: target score plus the AI roster,
 * with style and difficulty chosen per AI player. Online games can also start
 * with zero AIs and fill seats from the lobby.
 */
const Flip7SetupPage: React.FC = () => {
  const { mode } = useParams<{ mode: string }>();
  const isOnline = mode === 'online';
  const navigate = useNavigate();
  const { token } = useAppSelector((state) => state.auth);

  const [targetScore, setTargetScore] = useState(200);
  const [aiPlayers, setAiPlayers] = useState<Flip7AiSpec[]>(isOnline ? [] : [defaultAi(0), defaultAi(1)]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const updateAi = (index: number, patch: Partial<Flip7AiSpec>) =>
    setAiPlayers((list) => list.map((ai, i) => (i === index ? { ...ai, ...patch } : ai)));

  const addAi = () => setAiPlayers((list) => (list.length < MAX_AI ? [...list, defaultAi(list.length)] : list));

  const removeAi = (index: number) => setAiPlayers((list) => list.filter((_, i) => i !== index));

  const create = async () => {
    if (!token) return;
    if (!isOnline && aiPlayers.length === 0) {
      setError('Add at least one AI opponent.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const state = isOnline
        ? await flip7Api.createOnline(aiPlayers, targetScore, token)
        : await flip7Api.createVsAi(aiPlayers, targetScore, token);
      navigate(`/flip7/game/${state.id}`);
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  };

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.titleBlock}>
          <h1 className={styles.title}>
            Flip <span>7</span>
          </h1>
          <p className={styles.tagline}>{isOnline ? 'Online game setup' : 'Vs AI setup'}</p>
        </div>

        <div className={styles.setupCard}>
          <div className={styles.fieldRow}>
            <label className={styles.fieldLabel} htmlFor="target">
              Target score
            </label>
            <select
              id="target"
              className={styles.select}
              value={targetScore}
              onChange={(e) => setTargetScore(Number(e.target.value))}
            >
              <option value={100}>100 — quick</option>
              <option value={200}>200 — standard</option>
              <option value={300}>300 — marathon</option>
            </select>
          </div>

          <div className={styles.aiSection}>
            <div className={styles.aiHeader}>
              <h2>AI opponents</h2>
              <Button variant="ghost" size="sm" onClick={addAi} disabled={aiPlayers.length >= MAX_AI}>
                + Add AI
              </Button>
            </div>

            {aiPlayers.length === 0 && (
              <p className={styles.aiEmpty}>
                {isOnline
                  ? 'No AI players — human seats fill from the lobby. You can still add AIs.'
                  : 'Add at least one AI opponent.'}
              </p>
            )}

            {aiPlayers.map((ai, i) => (
              <div key={i} className={styles.aiRow}>
                <input
                  className={styles.aiName}
                  value={ai.username ?? ''}
                  maxLength={20}
                  onChange={(e) => updateAi(i, { username: e.target.value })}
                  aria-label={`AI ${i + 1} name`}
                />
                <select
                  className={styles.select}
                  value={ai.style}
                  onChange={(e) => updateAi(i, { style: e.target.value as Flip7AiStyle })}
                  aria-label={`AI ${i + 1} style`}
                >
                  {STYLES.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
                <select
                  className={styles.select}
                  value={ai.difficulty}
                  onChange={(e) => updateAi(i, { difficulty: e.target.value as Flip7AiDifficulty })}
                  aria-label={`AI ${i + 1} difficulty`}
                >
                  {DIFFICULTIES.map((d) => (
                    <option key={d} value={d}>
                      {d}
                    </option>
                  ))}
                </select>
                <button className={styles.removeBtn} onClick={() => removeAi(i)} aria-label="Remove AI">
                  ✕
                </button>
              </div>
            ))}

            <p className={styles.styleHint}>
              <strong>safe</strong> banks early · <strong>balanced</strong> plays the odds ·{' '}
              <strong>risky</strong> chases the Flip 7
            </p>
          </div>

          {error && <div className={styles.error}>{error}</div>}

          <div className={styles.footerRow}>
            <Button variant="ghost" size="md" onClick={() => navigate('/flip7')}>
              Back
            </Button>
            <Button variant="primary" size="lg" onClick={create} isLoading={busy}>
              {isOnline ? 'Create Lobby' : 'Start Game'}
            </Button>
          </div>
        </div>
      </div>
    </Layout>
  );
};

export default Flip7SetupPage;
