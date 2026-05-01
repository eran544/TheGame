import React from 'react';
import Pile from './Pile';
import styles from './GameBoard.module.css';
import type { PileSlot } from '../../types/game';

interface GameBoardProps {
  ascendingPiles: [number, number];
  descendingPiles: [number, number];
  validPileSlots: Set<PileSlot> | null;
  onPileClick: (slot: PileSlot) => void;
}

const GameBoard: React.FC<GameBoardProps> = ({
  ascendingPiles,
  descendingPiles,
  validPileSlots,
  onPileClick,
}) => {
  const pileValues = [
    ascendingPiles[0],
    ascendingPiles[1],
    descendingPiles[0],
    descendingPiles[1],
  ];

  return (
    <div className={styles.board}>
      {([0, 1, 2, 3] as PileSlot[]).map((slot) => (
        <Pile
          key={slot}
          slot={slot}
          topValue={pileValues[slot]}
          isActive={validPileSlots?.has(slot) ?? false}
          onClick={() => onPileClick(slot)}
        />
      ))}
    </div>
  );
};

export default GameBoard;
