import type { Flip7GameState, Flip7PlayerState } from '../types/flip7';
import { MODIFIER_LABELS } from '../types/flip7';

/**
 * The Flip 7 server broadcasts authoritative state snapshots rather than an
 * event log, so the feed is derived by diffing consecutive snapshots: new
 * cards in a line, status transitions, Second Chance gains/uses, Flip 7,
 * round and game end.
 */
export function deriveFlip7Events(
  prev: Flip7GameState | null,
  next: Flip7GameState
): string[] {
  const events: string[] = [];

  if (!prev || prev.id !== next.id) {
    if (next.status === 'Lobby') return [`Lobby open — waiting for players.`];
    return [`Round ${next.roundNumber} — cards are in the air.`];
  }

  if (next.roundNumber > prev.roundNumber) {
    events.push(`--- Round ${next.roundNumber} started ---`);
  }

  const prevById = new Map(prev.players.map((p) => [p.id, p]));
  for (const p of next.players) {
    const was = prevById.get(p.id);
    if (!was) {
      events.push(`${p.username} joined the game.`);
      continue;
    }
    events.push(...diffPlayer(was, p, next.roundNumber > prev.roundNumber));
  }

  if (next.roundEnded && !prev.roundEnded) {
    if (next.roundEndReason === 'Flip7') {
      const star = next.players.find((p) => p.achievedFlip7);
      if (star) events.push(`⭐ FLIP 7! ${star.username} flipped 7 unique numbers (+15)!`);
    }
    events.push(`--- Round over — scores banked ---`);
  }

  if (next.status === 'Completed' && prev.status !== 'Completed') {
    const winner = next.players.find((p) => p.id === next.winnerId);
    if (winner) events.push(`🏆 ${winner.username} wins with ${winner.cumulativeScore} points!`);
  }

  return events;
}

function diffPlayer(was: Flip7PlayerState, now: Flip7PlayerState, newRound: boolean): string[] {
  const events: string[] = [];
  const name = now.username;

  // On a new round all lines reset; report only fresh deals.
  const prevNumbers = newRound ? [] : was.numbers;
  const prevModifiers = newRound ? [] : was.modifiers;

  for (const n of addedItems(prevNumbers, now.numbers)) {
    events.push(`${name} flips a ${n}.`);
  }
  for (const m of addedItems(prevModifiers, now.modifiers)) {
    events.push(`${name} flips a ${MODIFIER_LABELS[m]} modifier.`);
  }

  const hadShield = newRound ? false : was.hasSecondChance;
  if (!hadShield && now.hasSecondChance) {
    events.push(`🛡️ ${name} holds a Second Chance.`);
  } else if (hadShield && !now.hasSecondChance && now.status === 'Active') {
    events.push(`🛡️ Second Chance saves ${name} from a duplicate!`);
  }

  const prevStatus = newRound ? 'Active' : was.status;
  if (prevStatus !== now.status) {
    if (now.status === 'Busted') events.push(`💥 ${name} BUSTS!`);
    if (now.status === 'Stayed') events.push(`${name} stays with ${now.roundScore} points.`);
    if (now.status === 'Frozen') events.push(`❄️ ${name} is frozen — ${now.roundScore} points banked.`);
  }

  return events;
}

/** Items appended to a line (multiset difference, order-insensitive). */
function addedItems<T>(before: readonly T[], after: readonly T[]): T[] {
  const counts = new Map<T, number>();
  for (const item of before) counts.set(item, (counts.get(item) ?? 0) + 1);
  const added: T[] = [];
  for (const item of after) {
    const left = counts.get(item) ?? 0;
    if (left > 0) counts.set(item, left - 1);
    else added.push(item);
  }
  return added;
}
