import React, { useEffect, useMemo } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import Layout from '../components/layout/Layout';
import GameBoard from '../components/game/GameBoard';
import PlayerHand from '../components/game/PlayerHand';
import GameStatus from '../components/game/GameStatus';
import GameEndModal from '../components/game/GameEndModal';
import ChatPanel from '../components/game/ChatPanel';
import Button from '../components/ui/Button';
import useAppDispatch from '../hooks/useAppDispatch';
import useAppSelector from '../hooks/useAppSelector';
import { useGameHub } from '../hooks/useGameHub';
import {
  playTurnAsync,
  loadGameAsync,
  selectCard,
  stagePlay,
  clearStagedPlays,
  clearGame,
  clearGameError,
} from '../store/slices/gameSlice';
import * as gameApi from '../api/gameApi';
import type { PileSlot } from '../types/game';
import styles from './MultiplayerGamePage.module.css';

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

const MultiplayerGamePage: React.FC = () => {
  const { sessionId } = useParams<{ sessionId: string }>();
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { token, user } = useAppSelector((s) => s.auth);
  const {
    gamePhase,
    ascendingPiles,
    descendingPiles,
    drawPileCount,
    playedCardsCount,
    playerHand,
    selectedCard,
    stagedPlays,
    finalScore,
    lastMove,
    isExpertMode,
    minCardsThisTurn,
    currentPlayerId,
    players,
    endReason,
    status,
    error,
  } = useAppSelector((s) => s.game);

  useGameHub(sessionId ?? null, token);

  useEffect(() => {
    if (token && sessionId) {
      dispatch(loadGameAsync({ sessionId, token }));
    }
    return () => { dispatch(clearGame()); };
  }, []);

  const isMyTurn = user?.id === currentPlayerId;

  // Apply staged plays to pile tops so subsequent cards in the same turn
  // are validated and displayed against the already-staged state.
  const effectivePiles = useMemo<{ asc: [number, number]; desc: [number, number] }>(() => {
    const asc: [number, number] = [ascendingPiles[0], ascendingPiles[1]];
    const desc: [number, number] = [descendingPiles[0], descendingPiles[1]];
    for (const sp of stagedPlays) {
      if (sp.pileSlot === 0) asc[0] = sp.card;
      else if (sp.pileSlot === 1) asc[1] = sp.card;
      else if (sp.pileSlot === 2) desc[0] = sp.card;
      else if (sp.pileSlot === 3) desc[1] = sp.card;
    }
    return { asc, desc };
  }, [ascendingPiles, descendingPiles, stagedPlays]);

  const validPileSlots = useMemo(
    () =>
      selectedCard !== null && isMyTurn
        ? getValidPileSlots(selectedCard, effectivePiles.asc, effectivePiles.desc)
        : null,
    [selectedCard, isMyTurn, effectivePiles]
  );

  const handlePileClick = (slot: PileSlot) => {
    if (selectedCard === null || !isMyTurn) return;
    if (!validPileSlots?.has(slot)) return;
    dispatch(stagePlay({ card: selectedCard, pileSlot: slot }));
  };

  const handleEndTurn = () => {
    if (!sessionId || !token || stagedPlays.length < minCardsThisTurn) return;
    dispatch(playTurnAsync({ sessionId, plays: stagedPlays, token }));
  };

  const handleUnstageLast = () => {
    if (stagedPlays.length === 0) return;
    dispatch(clearStagedPlays());
  };

  const handleLeave = async () => {
    if (!sessionId || !token) return;
    await gameApi.leaveGame(sessionId, token);
    dispatch(clearGame());
    navigate('/');
  };

  const handleBackToMenu = () => {
    dispatch(clearGame());
    navigate('/');
  };

  const currentPlayerName = players.find((p) => p.userId === currentPlayerId)?.username ?? '';

  if (status === 'loading' && gamePhase === 'lobby') {
    return (
      <Layout showHeader>
        <div className={styles.center}>
          <p className={styles.loadingText}>Loading game…</p>
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

        <div className={styles.topRow}>
          <GameStatus
            drawPileCount={drawPileCount}
            playedCardsCount={playedCardsCount}
            isExpertMode={isExpertMode}
          />
          <div className={styles.playerPanel}>
            {players.map((p) => (
              <div
                key={p.userId}
                className={[
                  styles.playerChip,
                  p.isCurrentTurn ? styles.playerChipActive : '',
                  p.isDisconnected ? styles.playerChipDc : '',
                ].join(' ')}
              >
                <span className={styles.chipName}>{p.username}</span>
                <span className={styles.chipCards}>{p.handCount}</span>
              </div>
            ))}
          </div>
        </div>

        <div className={styles.turnBanner}>
          {isMyTurn
            ? stagedPlays.length === 0
              ? 'Your turn — select a card'
              : stagedPlays.length < minCardsThisTurn
                ? `Play ${minCardsThisTurn - stagedPlays.length} more card${minCardsThisTurn - stagedPlays.length > 1 ? 's' : ''} to end turn`
                : 'Ready — click End Turn or play more'
            : status === 'loading'
              ? 'Waiting…'
              : `${currentPlayerName}'s turn`}
        </div>

        {lastMove && (
          <div className={styles.lastMove}>
            <span className={styles.lastMoveLabel}>{lastMove.playerUsername} played:</span>
            {lastMove.plays.map((p, i) => (
              <span key={i} className={styles.lastMoveTag}>
                {p.card} → {p.pileSlot < 2 ? '↑' : '↓'}{p.pileSlot % 2 + 1}
              </span>
            ))}
          </div>
        )}

        {stagedPlays.length > 0 && (
          <div className={styles.staged}>
            {stagedPlays.map((sp, i) => (
              <span key={i} className={styles.stagedTag}>
                {sp.card} → Pile {sp.pileSlot + 1}
              </span>
            ))}
          </div>
        )}

        <GameBoard
          ascendingPiles={effectivePiles.asc}
          descendingPiles={effectivePiles.desc}
          validPileSlots={isMyTurn ? validPileSlots : null}
          onPileClick={handlePileClick}
          isLoading={status === 'loading'}
        />

        <PlayerHand
          hand={playerHand}
          selectedCard={isMyTurn ? selectedCard : null}
          onSelectCard={(v) => isMyTurn && dispatch(selectCard(v))}
          isLoading={status === 'loading'}
        />

        <div className={styles.actions}>
          <Button
            variant="primary"
            onClick={handleEndTurn}
            disabled={!isMyTurn || stagedPlays.length < minCardsThisTurn || status === 'loading'}
          >
            End Turn ({stagedPlays.length}/{minCardsThisTurn})
          </Button>
          <Button
            variant="secondary"
            onClick={handleUnstageLast}
            disabled={stagedPlays.length === 0 || status === 'loading'}
          >
            Undo Stage
          </Button>
          <Button variant="danger" onClick={handleLeave} disabled={status === 'loading'}>
            Leave
          </Button>
        </div>

        {token && sessionId && (
          <ChatPanel sessionId={sessionId} token={token} />
        )}
      </div>

      {gamePhase === 'ended' && finalScore && (
        <GameEndModal
          score={finalScore}
          onPlayAgain={() => navigate('/multiplayer')}
          onBackToMenu={handleBackToMenu}
        />
      )}

      {gamePhase === 'ended' && !finalScore && (
        <div className={styles.disconnectOverlay}>
          <div className={styles.disconnectModal}>
            <p className={styles.disconnectTitle}>Game Over</p>
            <p className={styles.disconnectMessage}>
              {endReason === 'disconnection'
                ? 'A player disconnected. The game has ended.'
                : 'A player left. The game has ended.'}
            </p>
            <Button variant="primary" onClick={handleBackToMenu}>
              Back to Menu
            </Button>
          </div>
        </div>
      )}
    </Layout>
  );
};

export default MultiplayerGamePage;
