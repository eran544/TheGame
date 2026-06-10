import React, { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import Button from '../components/ui/Button';
import styles from './Flip7GamePage.module.css';

type CardType = 'number' | 'freeze' | 'second-chance' | 'flip-three' | 'add2' | 'add10' | 'double';

interface Flip7Card {
  id: string;
  type: CardType;
  value: number;
  label: string;
}

interface PlayerState {
  id: string;
  name: string;
  isAI: boolean;
  aiStyle?: 'cautious' | 'reckless';
  score: number;
  roundScore: number;
  line: Flip7Card[];
  isBusted: boolean;
  isStayed: boolean;
  flipThreeRemaining: number;
  hasSecondChance: boolean;
  secondChanceActive: boolean; // True if they have the shield, false if consumed
}

const INITIAL_PLAYERS: PlayerState[] = [
  {
    id: 'player',
    name: 'You',
    isAI: false,
    score: 0,
    roundScore: 0,
    line: [],
    isBusted: false,
    isStayed: false,
    flipThreeRemaining: 0,
    hasSecondChance: false,
    secondChanceActive: false,
  },
  {
    id: 'cautious-bot',
    name: 'Cautious Bot',
    isAI: true,
    aiStyle: 'cautious',
    score: 0,
    roundScore: 0,
    line: [],
    isBusted: false,
    isStayed: false,
    flipThreeRemaining: 0,
    hasSecondChance: false,
    secondChanceActive: false,
  },
  {
    id: 'reckless-bot',
    name: 'Reckless Bot',
    isAI: true,
    aiStyle: 'reckless',
    score: 0,
    roundScore: 0,
    line: [],
    isBusted: false,
    isStayed: false,
    flipThreeRemaining: 0,
    hasSecondChance: false,
    secondChanceActive: false,
  },
];

const Flip7GamePage: React.FC = () => {
  const navigate = useNavigate();

  const [players, setPlayers] = useState<PlayerState[]>(INITIAL_PLAYERS);
  const [deck, setDeck] = useState<Flip7Card[]>([]);
  const [gamePhase, setGamePhase] = useState<'setup' | 'playing' | 'round-end' | 'game-over'>('setup');
  const [activePlayerIndex, setActivePlayerIndex] = useState<number>(0);
  const [logs, setLogs] = useState<string[]>([]);
  const [roundNumber, setRoundNumber] = useState<number>(1);
  const [screenEffect, setScreenEffect] = useState<'none' | 'shake' | 'bust-flash' | 'win-flash'>('none');
  const [showBonusAlert, setShowBonusAlert] = useState<string | null>(null);

  const activePlayerRef = useRef<number>(0);
  activePlayerRef.current = activePlayerIndex;

  const logsEndRef = useRef<HTMLDivElement>(null);

  // Auto-scroll logs
  useEffect(() => {
    logsEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [logs]);

  // Create and shuffle deck
  const createDeck = (): Flip7Card[] => {
    const newDeck: Flip7Card[] = [];
    let idCounter = 0;

    // Number cards (0-12)
    // 0: 1 card
    newDeck.push({ id: `f7-${idCounter++}`, type: 'number', value: 0, label: '0' });
    // 1: 1 card
    newDeck.push({ id: `f7-${idCounter++}`, type: 'number', value: 1, label: '1' });
    
    // 2 to 12
    for (let val = 2; val <= 12; val++) {
      for (let count = 0; count < val; count++) {
        newDeck.push({ id: `f7-${idCounter++}`, type: 'number', value: val, label: String(val) });
      }
    }

    // Freeze: 3 cards
    for (let i = 0; i < 3; i++) {
      newDeck.push({ id: `f7-${idCounter++}`, type: 'freeze', value: 0, label: 'Freeze' });
    }
    // Second Chance: 3 cards
    for (let i = 0; i < 3; i++) {
      newDeck.push({ id: `f7-${idCounter++}`, type: 'second-chance', value: 0, label: '2nd Chance' });
    }
    // Flip Three: 3 cards
    for (let i = 0; i < 3; i++) {
      newDeck.push({ id: `f7-${idCounter++}`, type: 'flip-three', value: 0, label: 'Flip 3' });
    }
    // Modifiers
    // +2: 2 cards
    for (let i = 0; i < 2; i++) {
      newDeck.push({ id: `f7-${idCounter++}`, type: 'add2', value: 2, label: '+2' });
    }
    // +10: 2 cards
    for (let i = 0; i < 2; i++) {
      newDeck.push({ id: `f7-${idCounter++}`, type: 'add10', value: 10, label: '+10' });
    }
    // x2: 2 cards
    for (let i = 0; i < 2; i++) {
      newDeck.push({ id: `f7-${idCounter++}`, type: 'double', value: 2, label: 'x2' });
    }

    // Shuffle (Fisher-Yates)
    for (let i = newDeck.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      const temp = newDeck[i];
      newDeck[i] = newDeck[j];
      newDeck[j] = temp;
    }

    return newDeck;
  };

  const addLog = (msg: string) => {
    setLogs((prev) => [...prev, msg]);
  };

  const calculatePlayerRoundScore = (line: Flip7Card[], isBusted: boolean): number => {
    if (isBusted) return 0;
    let base = 0;
    let add = 0;
    let mult = 1;

    line.forEach((card) => {
      if (card.type === 'number') {
        base += card.value;
      } else if (card.type === 'add2') {
        add += 2;
      } else if (card.type === 'add10') {
        add += 10;
      } else if (card.type === 'double') {
        mult *= 2;
      }
    });

    return (base + add) * mult;
  };

  const startNewRound = (currentPlayersState = players) => {
    const freshDeck = createDeck();
    setLogs([]);
    addLog(`--- Round ${roundNumber} Started ---`);
    addLog(`Shuffling the deck... 94 cards ready.`);

    // Deal 1st card to everyone
    const updatedPlayers = currentPlayersState.map((p) => {
      const firstCard = freshDeck.pop();
      if (!firstCard) return p;

      const newLine = [firstCard];
      const hasSecChance = firstCard.type === 'second-chance';
      addLog(`${p.name} draws initial card: ${firstCard.label}`);

      return {
        ...p,
        line: newLine,
        isBusted: false,
        isStayed: false,
        flipThreeRemaining: firstCard.type === 'flip-three' ? 3 : 0,
        hasSecondChance: hasSecChance,
        secondChanceActive: hasSecChance,
        roundScore: calculatePlayerRoundScore(newLine, false),
      };
    });

    setDeck(freshDeck);
    setPlayers(updatedPlayers);
    setGamePhase('playing');
    setActivePlayerIndex(0);
  };

  const startNewGame = () => {
    const resetPlayers = INITIAL_PLAYERS.map((p) => ({ ...p, score: 0 }));
    setRoundNumber(1);
    setPlayers(resetPlayers);
    startNewRound(resetPlayers);
  };

  // Check if player has 7 unique number cards
  const checkFlip7Bonus = (line: Flip7Card[]): boolean => {
    const uniqueNumbers = new Set(
      line.filter((c) => c.type === 'number').map((c) => c.value)
    );
    return uniqueNumbers.size >= 7;
  };

  // Draw card helper
  const drawCardForPlayer = (playerIndex: number, currentDeck = deck): { card: Flip7Card; nextDeck: Flip7Card[] } => {
    const nextDeck = [...currentDeck];
    const card = nextDeck.pop();
    if (!card) {
      // If deck runs out, reshuffle a new one
      const newShuffledDeck = createDeck();
      const drawn = newShuffledDeck.pop()!;
      return { card: drawn, nextDeck: newShuffledDeck };
    }
    return { card, nextDeck };
  };

  // Main Hit Logic
  const handleHit = () => {
    if (gamePhase !== 'playing') return;
    const playerIndex = activePlayerIndex;
    const player = players[playerIndex];

    if (player.isStayed || player.isBusted) return;

    const { card, nextDeck } = drawCardForPlayer(playerIndex);
    setDeck(nextDeck);

    addLog(`${player.name} hits... Draws card: ${card.label}`);

    let newLine = [...player.line];
    let isBusted: boolean = player.isBusted;
    let isStayed: boolean = player.isStayed;
    let flipThreeRemaining = player.flipThreeRemaining;
    let hasSecondChance = player.hasSecondChance;
    let secondChanceActive = player.secondChanceActive;

    if (card.type === 'second-chance') {
      hasSecondChance = true;
      secondChanceActive = true;
      newLine.push(card);
    } else if (card.type === 'freeze') {
      isStayed = true;
      newLine.push(card);
      addLog(`❄️ ${player.name} is Frozen and must Stay!`);
    } else if (card.type === 'flip-three') {
      flipThreeRemaining = 3;
      newLine.push(card);
      addLog(`🔄 ${player.name} drew Flip 3! Must draw 3 more cards.`);
    } else if (card.type === 'number') {
      // Check if duplicate
      const duplicateExists = newLine.some(
        (c) => c.type === 'number' && c.value === card.value
      );

      if (duplicateExists) {
        if (hasSecondChance && secondChanceActive) {
          // Consume Second Chance shield
          secondChanceActive = false;
          addLog(`🛡️ Second Chance saves ${player.name}! The duplicate ${card.value} card is discarded.`);
        } else {
          // BUST!
          isBusted = true;
          addLog(`💥 BUST! ${player.name} drew a duplicate ${card.value}.`);
          if (!player.isAI) {
            setScreenEffect('bust-flash');
            setTimeout(() => setScreenEffect('none'), 800);
          }
        }
      } else {
        newLine.push(card);
      }
    } else {
      // Modifier cards (+2, +10, x2)
      newLine.push(card);
    }

    // Update flip three count if active
    if (flipThreeRemaining > 0 && card.type !== 'flip-three') {
      flipThreeRemaining--;
      if (flipThreeRemaining === 0) {
        addLog(`✅ ${player.name} completed the Flip 3 challenge.`);
      }
    }

    // Check Flip 7 Bonus
    const triggeredFlip7 = !isBusted && checkFlip7Bonus(newLine);

    const updatedRoundScore = calculatePlayerRoundScore(newLine, isBusted);

    const updatedPlayers = players.map((p, idx) => {
      if (idx !== playerIndex) return p;
      return {
        ...p,
        line: newLine,
        isBusted,
        isStayed: isStayed || isBusted,
        flipThreeRemaining,
        hasSecondChance,
        secondChanceActive,
        roundScore: updatedRoundScore,
      };
    });

    if (triggeredFlip7) {
      // Flip 7 ends round immediately
      addLog(`⭐ FLIP 7! ${player.name} collected 7 unique number cards! Round ends immediately for all.`);
      setShowBonusAlert(player.name);
      setScreenEffect('win-flash');
      setTimeout(() => setScreenEffect('none'), 1200);

      // Force stay on all other players
      const finalPlayers = updatedPlayers.map((p, idx) => {
        let bonus = 0;
        if (idx === playerIndex) {
          bonus = 15;
        }
        return {
          ...p,
          isStayed: true,
          roundScore: p.id === player.id ? p.roundScore + bonus : p.roundScore,
        };
      });

      setPlayers(finalPlayers);
      endRound(finalPlayers);
      return;
    }

    setPlayers(updatedPlayers);

    // If busted, frozen, or completed turn
    const isTurnFinished = isBusted || isStayed || (flipThreeRemaining === 0 && player.flipThreeRemaining > 0);

    if (isTurnFinished) {
      moveToNextPlayer(updatedPlayers);
    }
  };

  // Stay Logic
  const handleStay = () => {
    if (gamePhase !== 'playing') return;
    const playerIndex = activePlayerIndex;
    const player = players[playerIndex];

    if (player.isStayed || player.isBusted) return;
    if (player.flipThreeRemaining > 0) {
      addLog(`❌ Cannot Stay. You must complete your Flip 3 draws!`);
      return;
    }

    addLog(`${player.name} stays with ${player.roundScore} points.`);

    const updatedPlayers = players.map((p, idx) => {
      if (idx !== playerIndex) return p;
      return { ...p, isStayed: true };
    });

    setPlayers(updatedPlayers);
    moveToNextPlayer(updatedPlayers);
  };

  // Turn Navigation
  const moveToNextPlayer = (currentPlayersState = players) => {
    // Check if everyone has stayed/busted
    const allDone = currentPlayersState.every((p) => p.isStayed || p.isBusted);

    if (allDone) {
      endRound(currentPlayersState);
      return;
    }

    // Loop around to find next active player
    let nextIndex = (activePlayerIndex + 1) % currentPlayersState.length;
    while (currentPlayersState[nextIndex].isStayed || currentPlayersState[nextIndex].isBusted) {
      nextIndex = (nextIndex + 1) % currentPlayersState.length;
    }

    setActivePlayerIndex(nextIndex);
  };

  // End of Round calculations
  const endRound = (finalPlayers: PlayerState[]) => {
    addLog(`--- Round Ended ---`);
    const scoredPlayers = finalPlayers.map((p) => {
      const newTotal = p.score + p.roundScore;
      addLog(`${p.name} banks ${p.roundScore} points. Total: ${newTotal}`);
      return { ...p, score: newTotal };
    });

    setPlayers(scoredPlayers);

    // Check if any player won (reaches 200 points)
    const winners = scoredPlayers.filter((p) => p.score >= 200);
    if (winners.length > 0) {
      // Find highest score
      const sortedWinners = [...winners].sort((a, b) => b.score - a.score);
      addLog(`🏆 Game Over! ${sortedWinners[0].name} wins the game with ${sortedWinners[0].score} points!`);
      setGamePhase('game-over');
    } else {
      setGamePhase('round-end');
    }
  };

  // Proceed to next round
  const handleNextRound = () => {
    setShowBonusAlert(null);
    setRoundNumber((r) => r + 1);
    setGamePhase('playing');
    startNewRound(players);
  };

  // AI Logic Loop
  useEffect(() => {
    if (gamePhase !== 'playing') return;
    const activePlayer = players[activePlayerIndex];

    if (!activePlayer.isAI) return;

    // AI Turn delay for visual comfort
    const timer = setTimeout(() => {
      const p = activePlayer;
      const lineNumbers = p.line.filter((c) => c.type === 'number').map((c) => c.value);
      
      // Calculate duplicate probability
      // High frequency cards in deck: 12 (12 copies), 11 (11 copies), 10 (10 copies).
      // If AI already has high frequency numbers, bust chance increases.
      const hasHighCards = lineNumbers.some((n) => n >= 8);
      const uniqueCount = new Set(lineNumbers).size;

      let shouldHit = false;

      if (p.flipThreeRemaining > 0) {
        // AI MUST HIT if in flip-three
        shouldHit = true;
      } else {
        if (p.aiStyle === 'cautious') {
          // Cautious: Hit if less than 3 cards, or if score is under 14 and no high risk cards
          const riskOfBusting = hasHighCards || uniqueCount >= 3;
          if (p.roundScore < 14 && !riskOfBusting) {
            shouldHit = true;
          } else if (p.secondChanceActive) {
            // Safe shield is active
            shouldHit = p.roundScore < 18;
          }
        } else {
          // Reckless: Always hit if score is under 22, or if they have 5 unique numbers (chasing Flip 7)
          const isChasingFlip7 = uniqueCount >= 4;
          if (p.roundScore < 20 || (isChasingFlip7 && uniqueCount < 7)) {
            shouldHit = true;
          } else if (p.secondChanceActive) {
            shouldHit = p.roundScore < 26;
          }
        }
      }

      if (shouldHit) {
        handleHit();
      } else {
        handleStay();
      }
    }, 1200);

    return () => clearTimeout(timer);
  }, [activePlayerIndex, gamePhase, players]);

  // CSS Page Effect classes
  const pageClass = [
    styles.page,
    screenEffect === 'bust-flash' ? styles.effectBust : '',
    screenEffect === 'win-flash' ? styles.effectWin : '',
    screenEffect === 'shake' ? styles.effectShake : '',
  ].filter(Boolean).join(' ');

  return (
    <Layout showHeader>
      <div className={pageClass}>
        <div className={styles.headerArea}>
          <div className={styles.titleBlock}>
            <h1 className={styles.title}>
              Flip <span>7</span>
            </h1>
            <p className={styles.tagline}>Cyber-Neon Casino — Press Your Luck</p>
          </div>
          {gamePhase !== 'setup' && (
            <div className={styles.roundTracker}>Round {roundNumber}</div>
          )}
        </div>

        {gamePhase === 'setup' ? (
          <div className={styles.setupCard}>
            <h2>Race to 200 Points</h2>
            <p>
              Flip cards one by one. Accumulate numbers to score points. If you flip a
              duplicate number, you <strong>BUST</strong> (score 0 for the round).
            </p>
            <div className={styles.bulletList}>
              <div>❄️ <strong>Freeze:</strong> Ends your turn instantly to bank points.</div>
              <div>🛡️ <strong>2nd Chance:</strong> Discards your next duplicate.</div>
              <div>🔄 <strong>Flip 3:</strong> Forces you to flip three more cards.</div>
              <div>⭐ <strong>Flip 7 Bonus:</strong> Collect 7 unique numbers for +15 pts and end the round!</div>
            </div>
            <Button variant="primary" size="lg" onClick={startNewGame}>
              Start Game
            </Button>
          </div>
        ) : (
          <div className={styles.gameContainer}>
            {/* Scoreboard */}
            <div className={styles.scoreboard}>
              {players.map((p, idx) => {
                const isActive = activePlayerIndex === idx && gamePhase === 'playing';
                return (
                  <div
                    key={p.id}
                    className={[
                      styles.scoreCard,
                      isActive ? styles.activeScoreCard : '',
                      p.isBusted ? styles.bustedScoreCard : '',
                      p.isStayed && !p.isBusted ? styles.stayedScoreCard : '',
                    ].join(' ')}
                  >
                    <div className={styles.scoreHeader}>
                      <span className={styles.playerName}>{p.name}</span>
                      {p.isAI && <span className={styles.aiTag}>{p.aiStyle}</span>}
                    </div>
                    <div className={styles.scoreValues}>
                      <div className={styles.scoreGroup}>
                        <span className={styles.scoreLabel}>Total</span>
                        <span className={styles.scoreVal}>{p.score}</span>
                      </div>
                      <div className={styles.scoreGroup}>
                        <span className={styles.scoreLabel}>Round</span>
                        <span className={styles.roundVal}>
                          {p.isBusted ? 'BUST' : p.roundScore}
                        </span>
                      </div>
                    </div>
                    <div className={styles.playerStatusText}>
                      {p.isBusted && '💥 Busted'}
                      {p.isStayed && !p.isBusted && '🔒 Stayed'}
                      {isActive && '⚡ Active Turn'}
                      {p.flipThreeRemaining > 0 && `🔄 Draw ${p.flipThreeRemaining} more`}
                      {p.hasSecondChance && p.secondChanceActive && '🛡️ Shield Active'}
                      {p.hasSecondChance && !p.secondChanceActive && '🛡️ Shield Blown'}
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Board Panels */}
            <div className={styles.playboard}>
              {players.map((p) => (
                <div key={p.id} className={styles.playerBoardRow}>
                  <div className={styles.rowLabel}>{p.name}'s Line:</div>
                  <div className={styles.cardLine}>
                    {p.line.map((c) => {
                      let cardStyle = styles.numCard;
                      if (c.type === 'freeze') cardStyle = styles.freezeCard;
                      if (c.type === 'second-chance') cardStyle = styles.shieldCard;
                      if (c.type === 'flip-three') cardStyle = styles.challengeCard;
                      if (['add2', 'add10', 'double'].includes(c.type)) cardStyle = styles.modifierCard;

                      return (
                        <div key={c.id} className={cardStyle}>
                          <span className={styles.cornerPip}>{c.label}</span>
                          <span className={styles.mainValue}>{c.label}</span>
                        </div>
                      );
                    })}
                    {p.line.length === 0 && (
                      <span className={styles.waitingText}>Waiting to draw...</span>
                    )}
                  </div>
                </div>
              ))}
            </div>

            {/* Game Logs & Controls */}
            <div className={styles.interactionArea}>
              <div className={styles.logPanel}>
                <h3>Game Feed</h3>
                <div className={styles.logList}>
                  {logs.map((log, idx) => (
                    <div key={idx} className={styles.logItem}>
                      {log}
                    </div>
                  ))}
                  <div ref={logsEndRef} />
                </div>
              </div>

              {gamePhase === 'playing' && !players[activePlayerIndex].isAI && (
                <div className={styles.controls}>
                  <button
                    onClick={handleHit}
                    className={[styles.actionBtn, styles.hitBtn].join(' ')}
                    disabled={players[activePlayerIndex].isStayed}
                  >
                    HIT
                  </button>
                  <button
                    onClick={handleStay}
                    className={[styles.actionBtn, styles.stayBtn].join(' ')}
                    disabled={
                      players[activePlayerIndex].isStayed ||
                      players[activePlayerIndex].flipThreeRemaining > 0
                    }
                  >
                    STAY
                  </button>
                </div>
              )}

              {gamePhase === 'round-end' && (
                <div className={styles.roundControls}>
                  <div className={styles.roundSummaryAlert}>Round finished! Recap scores on the left.</div>
                  <Button variant="primary" size="lg" onClick={handleNextRound}>
                    Next Round
                  </Button>
                </div>
              )}

              {gamePhase === 'game-over' && (
                <div className={styles.roundControls}>
                  <div className={styles.roundSummaryAlert}>
                    🏆 Game Over! {players.slice().sort((a,b)=>b.score - a.score)[0].name} has won the match!
                  </div>
                  <Button variant="primary" size="lg" onClick={startNewGame}>
                    Play Again
                  </Button>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Bonus Modals */}
        {showBonusAlert && (
          <div className={styles.modalOverlay}>
            <div className={styles.modalContent}>
              <h2>⭐ FLIP 7 BONUS! ⭐</h2>
              <p>
                <strong>{showBonusAlert}</strong> successfully collected 7 unique number cards
                without busting!
              </p>
              <p className={styles.pointsGift}>+15 BONUS POINTS</p>
              <p>The round is completed.</p>
            </div>
          </div>
        )}
      </div>
    </Layout>
  );
};

export default Flip7GamePage;
