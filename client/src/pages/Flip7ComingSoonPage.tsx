import React from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import styles from './Flip7ComingSoonPage.module.css';

const Flip7ComingSoonPage: React.FC = () => {
  const navigate = useNavigate();

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.titleBlock}>
          <h1 className={styles.title}>
            Flip <span>7</span>
          </h1>
          <p className={styles.tagline}>
            A press-your-luck race to 200 points — with AI opponents you can tune
            from cautious to reckless, solo runs, and online play.
          </p>
          <p className={styles.badge}>Coming soon</p>
        </div>

        <div className={styles.actions}>
          <Button variant="secondary" size="lg" fullWidth onClick={() => navigate('/')}>
            Back to Games
          </Button>
        </div>
      </div>
    </Layout>
  );
};

export default Flip7ComingSoonPage;
