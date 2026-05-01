import React from 'react';
import styles from './Pile.module.css';
import type { PileSlot } from '../../types/game';

interface PileProps {
  slot: PileSlot;
  topValue: number;
  isActive: boolean;
  onClick: () => void;
}

const PILE_LABELS = ['ASC 1', 'ASC 2', 'DESC 1', 'DESC 2'];
const PILE_ARROWS = ['↑↑', '↑↑', '↓↓', '↓↓'];

const Pile: React.FC<PileProps> = ({ slot, topValue, isActive, onClick }) => {
  const isAscending = slot < 2;

  return (
    <button
      className={[
        styles.pile,
        isAscending ? styles.ascending : styles.descending,
        isActive ? styles.active : '',
      ]
        .filter(Boolean)
        .join(' ')}
      onClick={onClick}
      disabled={!isActive}
      aria-label={`${PILE_LABELS[slot]}, top card ${topValue}${isActive ? ', click to play' : ''}`}
    >
      <span className={styles.label}>{PILE_LABELS[slot]}</span>
      <span className={styles.arrow}>{PILE_ARROWS[slot]}</span>
      <span className={styles.topValue}>{topValue}</span>
    </button>
  );
};

export default Pile;
