import React from 'react';
import styles from './GameStatus.module.css';

interface GameStatusProps {
  drawPileCount: number;
  playedCardsCount: number;
  isExpertMode: boolean;
}

const GameStatus: React.FC<GameStatusProps> = ({
  drawPileCount,
  playedCardsCount,
  isExpertMode,
}) => (
  <div className={styles.status}>
    <div className={styles.stat}>
      <span className={styles.statValue}>{drawPileCount}</span>
      <span className={styles.statLabel}>Draw pile</span>
    </div>
    <div className={styles.divider} />
    <div className={styles.stat}>
      <span className={styles.statValue}>{playedCardsCount}</span>
      <span className={styles.statLabel}>Cards played</span>
    </div>
    {isExpertMode && (
      <>
        <div className={styles.divider} />
        <div className={styles.expertBadge}>Expert</div>
      </>
    )}
  </div>
);

export default GameStatus;
