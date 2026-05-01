import React, { useState } from 'react';
import styles from './BackwardsTrickDemo.module.css';

const PILE_START = 45;
const CARDS = [52, 46, 35]; // 35 is the backwards trick (45 - 10)

const BackwardsTrickDemo: React.FC = () => {
  const [pileTop, setPileTop] = useState(PILE_START);
  const [played, setPlayed] = useState<number | null>(null);
  const [animKey, setAnimKey] = useState(0);

  const isValid = (card: number) =>
    card > pileTop || card === pileTop - 10;

  const isTrick = (card: number) => card === pileTop - 10;

  const handlePlay = (card: number) => {
    if (!isValid(card) || played !== null) return;
    setPlayed(card);
    setAnimKey((k) => k + 1);
    setTimeout(() => setPileTop(card), 400);
  };

  const handleReset = () => {
    setPileTop(PILE_START);
    setPlayed(null);
  };

  const availableCards = CARDS.filter((c) => c !== played);

  return (
    <div className={styles.demo}>
      <div className={styles.board}>
        <div className={styles.pileArea}>
          <div className={styles.pileLabel}>ASC ↑↑</div>
          <div className={styles.pile}>
            <span key={animKey} className={styles.pileValue}>{pileTop}</span>
          </div>
        </div>

        <div className={styles.arrow}>→</div>

        <div className={styles.cards}>
          {availableCards.map((card) => (
            <button
              key={card}
              className={[
                styles.card,
                isTrick(card) ? styles.trick : '',
                !isValid(card) ? styles.invalid : '',
              ].filter(Boolean).join(' ')}
              onClick={() => handlePlay(card)}
              disabled={!isValid(card)}
              title={
                isTrick(card)
                  ? 'Backwards trick! 10 less than pile top'
                  : isValid(card)
                  ? 'Valid move'
                  : 'Invalid — too low'
              }
            >
              {card}
              {isTrick(card) && <span className={styles.trickBadge}>−10</span>}
            </button>
          ))}
          {availableCards.length === 0 && (
            <span className={styles.allPlayed}>All played!</span>
          )}
        </div>
      </div>

      {played !== null && (
        <p className={styles.result}>
          {isTrick(played)
            ? `Backwards trick! Card ${played} set the pile back from ${PILE_START} to ${played}.`
            : `Card ${played} played on the ascending pile.`}
        </p>
      )}

      <button className={styles.reset} onClick={handleReset}>
        ↺ Reset demo
      </button>
    </div>
  );
};

export default BackwardsTrickDemo;
