import React from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import useAppSelector from '../hooks/useAppSelector';
import styles from './MainMenuPage.module.css';

const MainMenuPage: React.FC = () => {
  const navigate = useNavigate();
  const { user } = useAppSelector((state) => state.auth);

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.titleBlock}>
          <h1 className={styles.title}>
            The <span>Game</span>
          </h1>
          <p className={styles.tagline}>
            Can you play all 98 cards?
          </p>
        </div>

        {user && (
          <p className={styles.welcome}>
            Welcome back, <strong>{user.username}</strong>
          </p>
        )}

        <div className={styles.actions}>
          <Button
            variant="primary"
            size="lg"
            fullWidth
            onClick={() => navigate('/game/new')}
          >
            Play Solo
          </Button>

          <Button
            variant="secondary"
            size="lg"
            fullWidth
            onClick={() => navigate('/multiplayer')}
          >
            Multiplayer
          </Button>

          <Button
            variant="secondary"
            size="lg"
            fullWidth
            onClick={() => navigate('/dashboard')}
          >
            Dashboard
          </Button>

          <Button
            variant="ghost"
            size="lg"
            fullWidth
            onClick={() => navigate('/instructions')}
          >
            How to Play
          </Button>
        </div>
      </div>
    </Layout>
  );
};

export default MainMenuPage;
