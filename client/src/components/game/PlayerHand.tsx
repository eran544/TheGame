import React from 'react';
import GameCard from '../ui/GameCard';
import styles from './PlayerHand.module.css';

interface PlayerHandProps {
  hand: number[];
  selectedCard: number | null;
  onSelectCard: (value: number) => void;
}

const PlayerHand: React.FC<PlayerHandProps> = ({ hand, selectedCard, onSelectCard }) => (
  <div className={styles.handArea}>
    <div className={styles.handLabel}>Your Hand</div>
    <div className={styles.hand}>
      {hand.map((value) => (
        <GameCard
          key={value}
          value={value}
          isSelected={selectedCard === value}
          onClick={() => onSelectCard(value)}
          size="md"
        />
      ))}
      {hand.length === 0 && (
        <span className={styles.emptyHand}>No cards in hand</span>
      )}
    </div>
  </div>
);

export default PlayerHand;
