import React from 'react';
import styles from './GameCard.module.css';

export interface GameCardProps {
  value: number;
  isSelected?: boolean;
  isPlayable?: boolean;
  isBackwardsTrick?: boolean;
  onClick?: () => void;
  size?: 'sm' | 'md' | 'lg';
}

const GameCard: React.FC<GameCardProps> = ({
  value,
  isSelected = false,
  isPlayable,
  isBackwardsTrick = false,
  onClick,
  size = 'md',
}) => {
  const isExplicitlyNotPlayable = isPlayable === false;

  const classes = [
    styles.card,
    styles[size],
    isSelected ? styles.selected : '',
    isPlayable === true && !isSelected ? styles.playable : '',
    isExplicitlyNotPlayable ? styles.notPlayable : '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div
      className={classes}
      onClick={isExplicitlyNotPlayable ? undefined : onClick}
      role={onClick && !isExplicitlyNotPlayable ? 'button' : undefined}
      tabIndex={onClick && !isExplicitlyNotPlayable ? 0 : undefined}
      aria-label={`Card ${value}${isSelected ? ', selected' : ''}${isBackwardsTrick ? ', backwards trick' : ''}`}
      aria-pressed={onClick ? isSelected : undefined}
      onKeyDown={
        onClick && !isExplicitlyNotPlayable
          ? (e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
    >
      {size !== 'sm' && (
        <span className={styles.cornerTL} aria-hidden="true">
          {value}
        </span>
      )}
      <span className={styles.value}>{value}</span>
      {size !== 'sm' && (
        <span className={styles.cornerBR} aria-hidden="true">
          {value}
        </span>
      )}
      {isBackwardsTrick && (
        <span
          className={styles.backwardsIndicator}
          aria-label="backwards trick available"
          title="Backwards trick"
        />
      )}
    </div>
  );
};

export default GameCard;
