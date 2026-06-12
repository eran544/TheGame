import React, { useEffect, useRef, useState } from 'react';
import Button from '../ui/Button';
import type { Flip7GameState, Flip7PlayerState } from '../../types/flip7';
import { MODIFIER_LABELS } from '../../types/flip7';
import styles from './Flip7Board.module.css';

interface Announcement {
  kind: 'flip7' | 'bust' | 'frozen';
  text: string;
  sub?: string;
}

interface Flip7BoardProps {
  state: Flip7GameState;
  /** The signed-in user's id (players are matched by userId). */
  myUserId: string;
  feed: string[];
  busy?: boolean;
  onHit: () => void;
  onStay: () => void;
  onNextRound: () => void;
  onExit: () => void;
}

/**
 * Renders one authoritative Flip7GameState: scoreboard, every player's line,
 * controls for the local player, and the round/game-end panels. Pure view —
 * all rules live on the server; the parent supplies the actions (REST for
 * solo, SignalR for multiplayer).
 */
const Flip7Board: React.FC<Flip7BoardProps> = ({
  state,
  myUserId,
  feed,
  busy = false,
  onHit,
  onStay,
  onNextRound,
  onExit,
}) => {
  const feedEndRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    feedEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [feed]);

  // Big transient overlays for the dramatic moments, derived by diffing
  // consecutive authoritative snapshots (mirrors the feed derivation).
  const [announcement, setAnnouncement] = useState<Announcement | null>(null);
  const prevRef = useRef<Flip7GameState | null>(null);
  useEffect(() => {
    const prev = prevRef.current;
    prevRef.current = state;
    if (!prev || prev.id !== state.id) return;

    const prevById = new Map(prev.players.map((p) => [p.id, p]));
    let next: Announcement | null = null;
    for (const p of state.players) {
      const was = prevById.get(p.id);
      if (!was) continue;
      if (p.achievedFlip7 && !was.achievedFlip7) {
        next = { kind: 'flip7', text: '⭐ FLIP 7! ⭐', sub: `${p.username} +15 bonus` };
        break;
      }
      if (p.status === 'Busted' && was.status !== 'Busted' && p.userId === myUserId && !p.isAi) {
        next = { kind: 'bust', text: '💥 BUST!', sub: 'Duplicate number — 0 this round' };
      } else if (p.status === 'Frozen' && was.status !== 'Frozen' && p.userId === myUserId && !p.isAi) {
        next = { kind: 'frozen', text: '❄️ FROZEN', sub: `${p.roundScore} points banked` };
      }
    }
    if (next) {
      setAnnouncement(next);
      const timer = setTimeout(() => setAnnouncement(null), 1800);
      return () => clearTimeout(timer);
    }
  }, [state, myUserId]);

  const me = state.players.find((p) => p.userId === myUserId && !p.isAi);
  const current = state.players.find((p) => p.id === state.currentPlayerId);
  const myTurn = !!me && !!current && current.id === me.id && !state.roundEnded;
  const gameOver = state.status === 'Completed';
  const winner = state.players.find((p) => p.id === state.winnerId);
  const canStay = myTurn && !!me && (me.numbers.length > 0 || me.modifiers.length > 0);

  const ranked = [...state.players].sort((a, b) => b.cumulativeScore - a.cumulativeScore);

  return (
    <div className={styles.gameContainer}>
      {/* Scoreboard */}
      <div className={styles.scoreboard}>
        {state.players.map((p) => {
          const isCurrent = !state.roundEnded && p.id === state.currentPlayerId;
          return (
            <div
              key={p.id}
              className={[
                styles.scoreCard,
                isCurrent ? styles.activeScoreCard : '',
                p.status === 'Busted' ? styles.bustedScoreCard : '',
                p.status === 'Stayed' || p.status === 'Frozen' ? styles.stayedScoreCard : '',
              ]
                .filter(Boolean)
                .join(' ')}
            >
              <div className={styles.scoreHeader}>
                <span className={styles.playerName}>
                  {p.username}
                  {me && p.id === me.id ? ' (you)' : ''}
                </span>
                {p.isAi && (
                  <span className={styles.aiTag}>
                    {p.aiStyle ?? 'ai'} · {p.aiDifficulty ?? 'medium'}
                  </span>
                )}
                {p.seat === state.dealerSeat && <span className={styles.dealerTag}>Dealer</span>}
              </div>
              <div className={styles.scoreValues}>
                <div className={styles.scoreGroup}>
                  <span className={styles.scoreLabel}>Total</span>
                  {/* Keying on the value remounts the span, replaying the pop. */}
                  <span key={p.cumulativeScore} className={styles.scoreVal}>
                    {p.cumulativeScore}
                  </span>
                </div>
                <div className={styles.scoreGroup}>
                  <span className={styles.scoreLabel}>Round</span>
                  <span key={`${p.roundScore}-${p.status}`} className={styles.roundVal}>
                    {p.status === 'Busted' ? 'BUST' : p.roundScore}
                  </span>
                </div>
              </div>
              <div className={styles.playerStatusText}>{statusText(p, isCurrent)}</div>
            </div>
          );
        })}
      </div>

      {/* Player lines */}
      <div className={styles.playboard}>
        {state.players.map((p) => (
          <div key={p.id} className={styles.playerBoardRow}>
            <div className={styles.rowLabel}>{p.username}&rsquo;s line</div>
            <div className={styles.cardLine}>
              {p.numbers.map((n, i) => (
                <div
                  key={`n-${i}`}
                  className={styles.numCard}
                  style={{ animationDelay: `${Math.min(i, 6) * 70}ms` }}
                >
                  <span className={styles.cornerPip}>{n}</span>
                  <span className={styles.mainValue}>{n}</span>
                </div>
              ))}
              {p.modifiers.map((m, i) => (
                <div
                  key={`m-${i}`}
                  className={styles.modifierCard}
                  style={{ animationDelay: `${Math.min(i, 6) * 70}ms` }}
                >
                  <span className={styles.cornerPip}>{MODIFIER_LABELS[m]}</span>
                  <span className={styles.mainValue}>{MODIFIER_LABELS[m]}</span>
                </div>
              ))}
              {p.hasSecondChance && (
                <div className={styles.shieldCard}>
                  <span className={styles.cornerPip}>🛡️</span>
                  <span className={styles.mainValue}>🛡️</span>
                </div>
              )}
              {p.numbers.length === 0 && p.modifiers.length === 0 && !p.hasSecondChance && (
                <span className={styles.waitingText}>No cards yet…</span>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Feed + controls */}
      <div className={styles.interactionArea}>
        <div className={styles.logPanel}>
          <h3>Game Feed</h3>
          <div className={styles.logList}>
            {feed.map((entry, i) => (
              <div key={i} className={styles.logItem}>
                {entry}
              </div>
            ))}
            <div ref={feedEndRef} />
          </div>
        </div>

        {!gameOver && !state.roundEnded && (
          <div className={styles.controls}>
            <button
              onClick={onHit}
              className={[styles.actionBtn, styles.hitBtn].join(' ')}
              disabled={!myTurn || busy}
            >
              Hit
            </button>
            <button
              onClick={onStay}
              className={[styles.actionBtn, styles.stayBtn].join(' ')}
              disabled={!canStay || busy}
            >
              Stay
            </button>
            {!myTurn && current && (
              <div className={styles.turnHint}>
                Waiting for <strong>{current.username}</strong>…
              </div>
            )}
          </div>
        )}

        {!gameOver && state.roundEnded && (
          <div className={styles.roundControls}>
            <div className={styles.roundSummaryAlert}>
              Round {state.roundNumber} finished
              {state.roundEndReason === 'Flip7' ? ' — FLIP 7!' : ''}
            </div>
            <Button variant="primary" size="lg" onClick={onNextRound} disabled={busy}>
              Next Round
            </Button>
          </div>
        )}

        {gameOver && (
          <div className={styles.roundControls}>
            <div className={styles.roundSummaryAlert}>
              🏆 {winner ? `${winner.username} wins with ${winner.cumulativeScore} points!` : 'Game over!'}
            </div>
            <div className={styles.finalRanking}>
              {ranked.map((p, i) => (
                <div key={p.id} className={styles.rankRow}>
                  <span>
                    {i + 1}. {p.username}
                  </span>
                  <span>{p.cumulativeScore}</span>
                </div>
              ))}
            </div>
            <Button variant="primary" size="lg" onClick={onExit}>
              Back to Flip 7 Menu
            </Button>
          </div>
        )}
      </div>

      {announcement && (
        <div
          className={[
            styles.announceOverlay,
            announcement.kind === 'flip7' ? styles.announceFlip7 : '',
            announcement.kind === 'bust' ? styles.announceBust : '',
            announcement.kind === 'frozen' ? styles.announceFrozen : '',
          ]
            .filter(Boolean)
            .join(' ')}
          aria-live="assertive"
        >
          <div className={styles.announceText}>{announcement.text}</div>
          {announcement.sub && <div className={styles.announceSub}>{announcement.sub}</div>}
        </div>
      )}
    </div>
  );
};

function statusText(p: Flip7PlayerState, isCurrent: boolean): string {
  if (p.achievedFlip7) return '⭐ FLIP 7!';
  if (p.status === 'Busted') return '💥 Busted';
  if (p.status === 'Frozen') return '❄️ Frozen (banked)';
  if (p.status === 'Stayed') return '🔒 Stayed';
  if (isCurrent) return '⚡ Taking turn';
  return '';
}

export default Flip7Board;
