import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import PileAnimation from '../components/instructions/PileAnimation';
import BackwardsTrickDemo from '../components/instructions/BackwardsTrickDemo';
import styles from './InstructionsPage.module.css';

interface Section {
  title: string;
  visual: React.ReactNode;
  body: React.ReactNode;
}

const CARD_SAMPLE = [3, 17, 35, 52, 78, 91];

const sections: Section[] = [
  {
    title: 'The Goal',
    visual: (
      <div className={styles.cardFan}>
        {CARD_SAMPLE.map((v) => (
          <div key={v} className={styles.fanCard}>{v}</div>
        ))}
        <div className={styles.fanEllipsis}>…98 cards total</div>
      </div>
    ),
    body: (
      <>
        <p>
          <strong>The Game</strong> is a cooperative challenge — play alone or team up.
          You have a shuffled deck of{' '}
          <strong>98 numbered cards</strong> (2–99) and four piles to play them on.
        </p>
        <p>
          Your goal: play <strong>all 98 cards</strong> before anyone runs out of valid moves.
          Simple to learn, brutally hard to master.
        </p>
      </>
    ),
  },
  {
    title: 'The Four Piles',
    visual: <PileAnimation />,
    body: (
      <>
        <p>
          There are <strong>two ascending piles</strong>{' '}
          <span className={styles.ascTag}>↑↑</span> that start at{' '}
          <strong>1</strong> and count upward, and{' '}
          <strong>two descending piles</strong>{' '}
          <span className={styles.descTag}>↓↓</span> that start at{' '}
          <strong>100</strong> and count downward.
        </p>
        <p>
          Each pile only shows its <strong>top card</strong>. The values above
          cycle to show cards being played over time.
        </p>
      </>
    ),
  },
  {
    title: 'Playing Cards',
    visual: (
      <div className={styles.playDemo}>
        <div className={styles.demoHand}>
          {[18, 42, 67].map((v, i) => (
            <div
              key={v}
              className={[styles.demoCard, i === 1 ? styles.demoSelected : ''].join(' ')}
            >
              {v}
            </div>
          ))}
        </div>
        <div className={styles.demoArrow}>↓</div>
        <div className={styles.demoPiles}>
          <div className={[styles.demoPile, styles.demoAsc].join(' ')}>
            <span className={styles.demoPileLabel}>ASC ↑↑</span>
            <span className={styles.demoPileVal}>38</span>
          </div>
          <div className={[styles.demoPile, styles.demoAscActive].join(' ')}>
            <span className={styles.demoPileLabel}>ASC ↑↑</span>
            <span className={styles.demoPileVal}>29</span>
          </div>
          <div className={[styles.demoPile, styles.demoDesc].join(' ')}>
            <span className={styles.demoPileLabel}>DESC ↓↓</span>
            <span className={styles.demoPileVal}>71</span>
          </div>
          <div className={[styles.demoPile, styles.demoDesc].join(' ')}>
            <span className={styles.demoPileLabel}>DESC ↓↓</span>
            <span className={styles.demoPileVal}>55</span>
          </div>
        </div>
      </div>
    ),
    body: (
      <>
        <p>
          <strong>Click a card</strong> in your hand to select it.
          Valid piles will <strong>light up</strong> — only piles where
          that card can legally be played will be clickable.
        </p>
        <p>
          Click a highlighted pile to play the card instantly.
          Card <strong>42</strong> can go on the pile showing <strong>29</strong>{' '}
          (42 &gt; 29 ✓) but not on <strong>38</strong> or <strong>71</strong>.
        </p>
      </>
    ),
  },
  {
    title: 'The Backwards Trick  ±10',
    visual: <BackwardsTrickDemo />,
    body: (
      <>
        <p>
          The game's secret weapon: on an <span className={styles.ascTag}>ascending</span> pile,
          you can play a card that is <strong>exactly 10 lower</strong> than the top.
          On a <span className={styles.descTag}>descending</span> pile,{' '}
          <strong>exactly 10 higher</strong>.
        </p>
        <p>
          Try it above — click card <strong className={styles.trickHighlight}>35</strong>{' '}
          (the gold one) to reset the pile from 45 back to 35. This creates
          breathing room to play more cards.
        </p>
      </>
    ),
  },
  {
    title: 'Scoring',
    visual: (
      <div className={styles.ratings}>
        <div className={[styles.rating, styles.ratingPerfect].join(' ')}>
          <span className={styles.ratingEmoji}>🏆</span>
          <span className={styles.ratingLabel}>Perfect</span>
          <span className={styles.ratingDesc}>0 cards remaining</span>
        </div>
        <div className={[styles.rating, styles.ratingExcellent].join(' ')}>
          <span className={styles.ratingEmoji}>⭐</span>
          <span className={styles.ratingLabel}>Excellent</span>
          <span className={styles.ratingDesc}>1–9 remaining</span>
        </div>
        <div className={[styles.rating, styles.ratingTryAgain].join(' ')}>
          <span className={styles.ratingEmoji}>🎴</span>
          <span className={styles.ratingLabel}>Try Again</span>
          <span className={styles.ratingDesc}>10+ remaining</span>
        </div>
      </div>
    ),
    body: (
      <>
        <p>
          The game ends when you <strong>can no longer play any card</strong>{' '}
          on any pile. Your score is the number of cards left in your hand
          and draw pile — <strong>lower is better</strong>.
        </p>
        <p>
          Play all 98 cards for a <strong className={styles.perfectHighlight}>Perfect Game</strong>.
          Good luck — you'll need it!
        </p>
      </>
    ),
  },
];

const InstructionsPage: React.FC = () => {
  const navigate = useNavigate();
  const [current, setCurrent] = useState(0);

  const prev = () => setCurrent((i) => Math.max(0, i - 1));
  const next = () => setCurrent((i) => Math.min(sections.length - 1, i + 1));

  const section = sections[current];

  return (
    <Layout showHeader>
      <div className={styles.page}>
        <div className={styles.header}>
          <h2 className={styles.pageTitle}>How to Play</h2>
          <p className={styles.progress}>
            {current + 1} / {sections.length}
          </p>
        </div>

        <div className={styles.card}>
          <h3 className={styles.sectionTitle}>{section.title}</h3>

          <div className={styles.visual}>{section.visual}</div>

          <div className={styles.body}>{section.body}</div>
        </div>

        <div className={styles.dots}>
          {sections.map((_, i) => (
            <button
              key={i}
              className={[styles.dot, i === current ? styles.dotActive : ''].join(' ')}
              onClick={() => setCurrent(i)}
              aria-label={`Go to section ${i + 1}`}
            />
          ))}
        </div>

        <div className={styles.nav}>
          <Button
            variant="secondary"
            onClick={prev}
            disabled={current === 0}
          >
            ← Previous
          </Button>
          {current < sections.length - 1 ? (
            <Button variant="primary" onClick={next}>
              Next →
            </Button>
          ) : (
            <Button variant="primary" onClick={() => navigate('/')}>
              Back to Menu
            </Button>
          )}
        </div>

        {current > 0 && (
          <button className={styles.backLink} onClick={() => navigate('/')}>
            ← Back to Menu
          </button>
        )}
      </div>
    </Layout>
  );
};

export default InstructionsPage;
