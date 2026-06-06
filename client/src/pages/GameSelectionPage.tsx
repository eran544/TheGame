import React from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import useAppSelector from '../hooks/useAppSelector';
import styles from './GameSelectionPage.module.css';

interface GameTile {
  key: string;
  title: string;
  accent: string;
  tagline: string;
  route: string;
  available: boolean;
}

const GAMES: GameTile[] = [
  {
    key: 'the-game',
    title: 'The Game',
    accent: 'Game',
    tagline: 'Cooperative — play all 98 cards together.',
    route: '/the-game',
    available: true,
  },
  {
    key: 'flip7',
    title: 'Flip 7',
    accent: '7',
    tagline: 'Press your luck — race to 200 points.',
    route: '/flip7',
    available: true,
  },
];

const GameSelectionPage: React.FC = () => {
  const navigate = useNavigate();
  const { user } = useAppSelector((state) => state.auth);

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.titleBlock}>
          <h1 className={styles.title}>Choose a Game</h1>
          {user && (
            <p className={styles.welcome}>
              Welcome back, <strong>{user.username}</strong>
            </p>
          )}
        </div>

        <div className={styles.grid}>
          {GAMES.map((game) => (
            <button
              key={game.key}
              type="button"
              className={styles.card}
              onClick={() => navigate(game.route)}
            >
              <span className={styles.cardTitle}>{game.title}</span>
              <span className={styles.cardTagline}>{game.tagline}</span>
            </button>
          ))}
        </div>
      </div>
    </Layout>
  );
};

export default GameSelectionPage;
