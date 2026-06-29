import React, { useEffect, useRef, useState } from 'react';
import Button from '../ui/Button';
import type { Flip7GameState, Flip7PlayerState } from '../../types/flip7';
import { MODIFIER_LABELS } from '../../types/flip7';
import { describeEvent } from '../../utils/flip7Events';
import styles from './Flip7Board.module.css';

interface Flip7BoardProps {
  state: Flip7GameState;
  /** The signed-in user's id (players are matched by userId). */
  myUserId: string;
  busy?: boolean;
  onHit: () => void;
  onStay: () => void;
  onChooseTarget: (targetPlayerId: string) => void;
  onNextRound: () => void;
  onExit: () => void;
}

interface Announcement {
  kind: 'flip7' | 'bust' | 'frozen';
  text: string;
  sub?: string;
}

/**
 * Renders one authoritative Flip7GameState: scoreboard, every player's line,
 * controls, the action-card target picker, and round/game-end panels. The feed
 * and the big celebration overlays are built from the server's event stream
 * (keyed by actionId so reconnects don't replay) — no client-side game logic.
 */
const Flip7Board: React.FC<Flip7BoardProps> = ({
  state,
  myUserId,
  busy = false,
  onHit,
  onStay,
  onChooseTarget,
  onNextRound,
  onExit,
}) => {
  const me = state.players.find((p) => p.userId === myUserId && !p.isAi);
  const current = state.players.find((p) => p.id === state.currentPlayerId);
  const gameOver = state.status === 'Completed';
  const winner = state.players.find((p) => p.id === state.winnerId);

  const pending = state.pendingAction ?? null;
  const iMustChoose = !!pending && !!me && pending.drawerId === me.id;
  const myTurn =
    !!me && !!current && current.id === me.id && !state.roundEnded && !pending;
  const canStay = myTurn && (me!.numbers.length > 0 || me!.modifiers.length > 0);

  // ---- Feed + announcements from the server event stream ----------------
  const [feed, setFeed] = useState<string[]>([]);
  const [announcement, setAnnouncement] = useState<Announcement | null>(null);
  const lastActionRef = useRef<string | null>(null);
  const feedEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const actionId = state.actionId ?? null;
    const events = state.events ?? [];
    // Only process a given action's events once (guards reconnect/replay).
    if (!actionId || actionId === lastActionRef.current || events.length === 0) return;
    lastActionRef.current = actionId;

    const lines = events.map((e) => describeEvent(e, state)).filter((l): l is string => !!l);
    if (lines.length) setFeed((old) => [...old, ...lines]);

    // Pick the most dramatic beat for the overlay. Busts fire for any player so
    // you see opponents (including AI) go down, not just yourself.
    const nameOf = (id: string) => state.players.find((p) => p.id === id)?.username ?? '';
    const isMine = (id: string) => state.players.find((p) => p.id === id)?.userId === myUserId;
    const flip7 = events.find((e) => e.type === 'Flip7Achieved');
    const bust = events.find((e) => e.type === 'Busted');
    const myFrozen = events.find((e) => e.type === 'Frozen' && isMine(e.playerId));
    if (flip7) {
      setAnnouncement({ kind: 'flip7', text: '⭐ FLIP 7! ⭐', sub: `${nameOf(flip7.playerId)} +15 bonus` });
    } else if (bust) {
      setAnnouncement({
        kind: 'bust',
        text: '💥 BUST!',
        sub: isMine(bust.playerId)
          ? `Duplicate ${bust.card} — 0 this round`
          : `${nameOf(bust.playerId)} busts on a duplicate ${bust.card}`,
      });
    } else if (myFrozen) {
      setAnnouncement({ kind: 'frozen', text: '❄️ FROZEN', sub: 'Points banked' });
    }
  }, [state, myUserId]);

  useEffect(() => {
    if (!announcement) return;
    const t = setTimeout(() => setAnnouncement(null), 1800);
    return () => clearTimeout(t);
  }, [announcement]);

  useEffect(() => {
    feedEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [feed]);

  const ranked = [...state.players].sort((a, b) => b.cumulativeScore - a.cumulativeScore);

  return (
    <div className={styles.gameContainer}>
      {/* Scoreboard */}
      <div className={styles.scoreboard}>
        {state.players.map((p) => {
          const isCurrent = !state.roundEnded && !pending && p.id === state.currentPlayerId;
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
          <div
            key={p.id}
            className={[
              styles.playerBoardRow,
              pending && p.id === pending.drawerId ? styles.pickingRow : '',
            ]
              .filter(Boolean)
              .join(' ')}
          >
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
              {p.status === 'Frozen' && (
                <div className={styles.freezeCard} title="Frozen this round">
                  <span className={styles.cornerPip}>❄️</span>
                  <span className={styles.mainValue}>❄️</span>
                </div>
              )}
              {p.status === 'Busted' && p.bustedNumber != null && (
                <div className={styles.bustCard} title={`Busted on a duplicate ${p.bustedNumber}`}>
                  <span className={styles.cornerPip}>💥</span>
                  <span className={styles.mainValue}>{p.bustedNumber}</span>
                </div>
              )}
              {p.numbers.length === 0 &&
                p.modifiers.length === 0 &&
                !p.hasSecondChance &&
                p.status !== 'Frozen' &&
                p.status !== 'Busted' && <span className={styles.waitingText}>No cards yet…</span>}
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
            {pending && !iMustChoose && current && (
              <div className={styles.turnHint}>
                <strong>{state.players.find((p) => p.id === pending.drawerId)?.username}</strong> is
                choosing a target…
              </div>
            )}
            {!pending && !myTurn && current && (
              <div className={styles.turnHint}>
                Waiting for <strong>{current.username}</strong>…
              </div>
            )}
            {!pending && !current && (
              <div className={styles.turnHint}>Dealing the round…</div>
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

      {/* Action-card target picker (the drawer chooses; others are shown disabled) */}
      {iMustChoose && pending && (
        <TargetPicker
          state={state}
          pending={pending}
          busy={busy}
          onChoose={onChooseTarget}
        />
      )}

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

const ACTION_COPY = {
  Freeze: { verb: 'Freeze', emoji: '❄️', desc: 'banks their points and ends their round' },
  FlipThree: { verb: 'Flip Three', emoji: '🔄', desc: 'forces them to draw three cards' },
} as const;

const TargetPicker: React.FC<{
  state: Flip7GameState;
  pending: NonNullable<Flip7GameState['pendingAction']>;
  busy: boolean;
  onChoose: (id: string) => void;
}> = ({ state, pending, busy, onChoose }) => {
  const copy = ACTION_COPY[pending.action];
  const candidateSet = new Set(pending.candidateIds);
  const onlySelf =
    pending.candidateIds.length === 1 && pending.candidateIds[0] === pending.drawerId;

  return (
    <div className={styles.pickerOverlay} aria-live="assertive">
      <div className={styles.pickerCard}>
        <div className={styles.pickerCardArt}>{copy.emoji}</div>
        <h2 className={styles.pickerTitle}>You drew {copy.verb}</h2>
        <p className={styles.pickerSub}>
          {onlySelf ? (
            <>You&rsquo;re the only active player — you must {copy.verb} yourself.</>
          ) : (
            <>Choose who to {copy.verb} — it {copy.desc}.</>
          )}
        </p>

        <div className={styles.pickerPlayers}>
          {state.players.map((p) => {
            const selectable = candidateSet.has(p.id) && !busy;
            const isSelf = p.id === pending.drawerId;
            return (
              <button
                key={p.id}
                className={[
                  styles.pickerPlayer,
                  selectable ? styles.pickerSelectable : styles.pickerDisabled,
                  isSelf ? styles.pickerSelf : '',
                ]
                  .filter(Boolean)
                  .join(' ')}
                disabled={!selectable}
                onClick={() => selectable && onChoose(p.id)}
              >
                <span className={styles.pickerPlayerName}>
                  {p.username}
                  {isSelf ? ' (you)' : ''}
                </span>
                <span className={styles.pickerPlayerMeta}>
                  {p.status === 'Active'
                    ? `${p.numbers.length} cards`
                    : p.status.toLowerCase()}
                </span>
              </button>
            );
          })}
        </div>
      </div>
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
