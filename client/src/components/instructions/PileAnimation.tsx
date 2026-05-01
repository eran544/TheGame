import React, { useEffect, useState } from 'react';
import styles from './PileAnimation.module.css';

// Four independent sequences — all values unique across all piles, each sequence
// strictly ascending or descending to reflect legal game moves.
// Each pile has its own start delay AND its own interval period so they never
// all update at the same time.
const PILE_CONFIGS = [
  { isAscending: true,  seq: [6,  17, 28, 41, 54], delay: 0,    period: 1600 },
  { isAscending: true,  seq: [11, 25, 37, 49, 63], delay: 900,  period: 2300 },
  { isAscending: false, seq: [94, 83, 71, 58, 45], delay: 400,  period: 1950 },
  { isAscending: false, seq: [98, 88, 74, 65, 52], delay: 1300, period: 2600 },
];

interface AnimatedPileProps {
  isAscending: boolean;
  seq: number[];
  delay: number;
  period: number;
}

const AnimatedPile: React.FC<AnimatedPileProps> = ({ isAscending, seq, delay, period }) => {
  const [idx, setIdx] = useState(0);
  const [key, setKey] = useState(0);

  useEffect(() => {
    let intervalId: ReturnType<typeof setInterval>;
    const timeoutId = setTimeout(() => {
      intervalId = setInterval(() => {
        setIdx((i) => (i + 1) % seq.length);
        setKey((k) => k + 1);
      }, period);
    }, delay);
    return () => {
      clearTimeout(timeoutId);
      clearInterval(intervalId);
    };
  }, [seq.length, delay, period]);

  return (
    <div className={[styles.pile, isAscending ? styles.asc : styles.desc].join(' ')}>
      <span className={styles.label}>{isAscending ? 'ASC ↑↑' : 'DESC ↓↓'}</span>
      <span key={key} className={styles.value}>{seq[idx]}</span>
      <span className={styles.start}>{isAscending ? 'starts at 1' : 'starts at 100'}</span>
    </div>
  );
};

const PileAnimation: React.FC = () => (
  <div className={styles.row}>
    {PILE_CONFIGS.map((cfg, i) => (
      <AnimatedPile key={i} {...cfg} />
    ))}
  </div>
);

export default PileAnimation;
