import React from 'react';
import Pile from './Pile';
import styles from './GameBoard.module.css';
import type { PileSlot } from '../../types/game';

interface GameBoardProps {
  ascendingPiles: [number, number];
  descendingPiles: [number, number];
  validPileSlots: Set<PileSlot> | null;
  onPileClick: (slot: PileSlot) => void;
  isLoading?: boolean;
}

const GameBoard: React.FC<GameBoardProps> = ({
  ascendingPiles,
  descendingPiles,
  validPileSlots,
  onPileClick,
  isLoading = false,
}) => {
  const pileValues = [
    ascendingPiles[0],
    ascendingPiles[1],
    descendingPiles[0],
    descendingPiles[1],
  ];

  return (
    <div className={[styles.board, isLoading ? styles.boardLoading : ''].join(' ')}>
      {([0, 1, 2, 3] as PileSlot[]).map((slot) => (
        <Pile
          key={slot}
          slot={slot}
          topValue={pileValues[slot]}
          isActive={!isLoading && (validPileSlots?.has(slot) ?? false)}
          onClick={() => !isLoading && onPileClick(slot)}
        />
      ))}
    </div>
  );
};

export default GameBoard;
