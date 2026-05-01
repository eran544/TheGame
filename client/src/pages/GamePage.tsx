import React, { useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import GameBoard from '../components/game/GameBoard';
import PlayerHand from '../components/game/PlayerHand';
import GameStatus from '../components/game/GameStatus';
import GameEndModal from '../components/game/GameEndModal';
import Button from '../components/ui/Button';
import useAppDispatch from '../hooks/useAppDispatch';
import useAppSelector from '../hooks/useAppSelector';
import {
  startGameAsync,
  playTurnAsync,
  abandonGameAsync,
  selectCard,
  clearGame,
  clearGameError,
} from '../store/slices/gameSlice';
import type { PileSlot } from '../types/game';
import styles from './GamePage.module.css';

function getValidPileSlots(
  card: number,
  ascendingPiles: [number, number],
  descendingPiles: [number, number]
): Set<PileSlot> {
  const valid = new Set<PileSlot>();
  if (card > ascendingPiles[0] || card === ascendingPiles[0] - 10) valid.add(0);
  if (card > ascendingPiles[1] || card === ascendingPiles[1] - 10) valid.add(1);
  if (card < descendingPiles[0] || card === descendingPiles[0] + 10) valid.add(2);
  if (card < descendingPiles[1] || card === descendingPiles[1] + 10) valid.add(3);
  return valid;
}

const GamePage: React.FC = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { token } = useAppSelector((s) => s.auth);
  const {
    sessionId,
    gamePhase,
    ascendingPiles,
    descendingPiles,
    drawPileCount,
    playedCardsCount,
    playerHand,
    selectedCard,
    finalScore,
    isExpertMode,
    status,
    error,
  } = useAppSelector((s) => s.game);

  useEffect(() => {
    if (token) dispatch(startGameAsync({ isExpertMode: false, token }));
    return () => { dispatch(clearGame()); };
  }, []);

  const validPileSlots = useMemo(
    () =>
      selectedCard !== null
        ? getValidPileSlots(selectedCard, ascendingPiles, descendingPiles)
        : null,
    [selectedCard, ascendingPiles, descendingPiles]
  );

  const handlePileClick = (slot: PileSlot) => {
    if (selectedCard === null || !sessionId || !token) return;
    if (!validPileSlots?.has(slot)) return;
    dispatch(playTurnAsync({ sessionId, plays: [{ card: selectedCard, pileSlot: slot }], token }));
  };

  const handleAbandon = () => {
    if (!sessionId || !token) return;
    dispatch(abandonGameAsync({ sessionId, token }));
  };

  const handlePlayAgain = () => {
    dispatch(clearGame());
    if (token) dispatch(startGameAsync({ isExpertMode: false, token }));
  };

  const handleBackToMenu = () => {
    dispatch(clearGame());
    navigate('/');
  };

  if (status === 'loading' && gamePhase === 'lobby') {
    return (
      <Layout showHeader>
        <div className={styles.center}>
          <p className={styles.loadingText}>Starting game…</p>
        </div>
      </Layout>
    );
  }

  return (
    <Layout showHeader>
      <div className={styles.page}>
        {error && (
          <div className={styles.errorBanner}>
            <span>{error}</span>
            <button onClick={() => dispatch(clearGameError())} className={styles.errorClose}>✕</button>
          </div>
        )}

        <GameStatus
          drawPileCount={drawPileCount}
          playedCardsCount={playedCardsCount}
          isExpertMode={isExpertMode}
        />

        <div className={styles.instruction}>
          {selectedCard === null
            ? 'Select a card from your hand'
            : validPileSlots?.size === 0
              ? `Card ${selectedCard} has no valid moves — pick a different card`
              : `Card ${selectedCard} selected — click a highlighted pile to play it`}
        </div>

        <GameBoard
          ascendingPiles={ascendingPiles}
          descendingPiles={descendingPiles}
          validPileSlots={validPileSlots}
          onPileClick={handlePileClick}
        />

        <PlayerHand
          hand={playerHand}
          selectedCard={selectedCard}
          onSelectCard={(v) => dispatch(selectCard(v))}
        />

        <div className={styles.actions}>
          <Button variant="danger" onClick={handleAbandon} disabled={status === 'loading'}>
            Abandon
          </Button>
        </div>
      </div>

      {gamePhase === 'ended' && finalScore && (
        <GameEndModal
          score={finalScore}
          onPlayAgain={handlePlayAgain}
          onBackToMenu={handleBackToMenu}
        />
      )}
    </Layout>
  );
};

export default GamePage;
