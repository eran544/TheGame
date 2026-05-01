import React from 'react';
import Button from '../ui/Button';
import styles from './GameEndModal.module.css';
import type { FinalScore } from '../../types/game';

interface GameEndModalProps {
  score: FinalScore;
  onPlayAgain: () => void;
  onBackToMenu: () => void;
}

const RATING_CONFIG = {
  Perfect: { label: 'Perfect Game!', emoji: '🏆', className: 'perfect' },
  Excellent: { label: 'Excellent!', emoji: '⭐', className: 'excellent' },
  TryAgain: { label: 'Try Again', emoji: '🎴', className: 'tryAgain' },
};

const GameEndModal: React.FC<GameEndModalProps> = ({ score, onPlayAgain, onBackToMenu }) => {
  const config = RATING_CONFIG[score.rating] ?? RATING_CONFIG.TryAgain;

  return (
    <div className={styles.overlay}>
      <div className={styles.modal}>
        <div className={[styles.ratingBadge, styles[config.className]].join(' ')}>
          <span className={styles.emoji}>{config.emoji}</span>
          <span className={styles.ratingLabel}>{config.label}</span>
        </div>

        {score.isPerfectGame ? (
          <p className={styles.message}>You played all 98 cards!</p>
        ) : (
          <p className={styles.message}>
            <span className={styles.cardsLeft}>{score.cardsRemaining}</span>
            {' '}card{score.cardsRemaining !== 1 ? 's' : ''} remaining
          </p>
        )}

        <div className={styles.actions}>
          <Button variant="primary" onClick={onPlayAgain}>
            Play Again
          </Button>
          <Button variant="secondary" onClick={onBackToMenu}>
            Back to Menu
          </Button>
        </div>
      </div>
    </div>
  );
};

export default GameEndModal;
