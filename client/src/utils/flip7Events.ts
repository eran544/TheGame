import type { Flip7Event, Flip7GameState } from '../types/flip7';

/**
 * The Flip 7 server emits an authoritative ordered event list with every
 * state-changing action (each card reveal, bust with its duplicate value,
 * freeze, etc.), tagged with a unique actionId. These helpers turn those into
 * human-readable feed lines — no client-side game logic or snapshot diffing.
 */
export function describeEvent(e: Flip7Event, state: Flip7GameState): string | null {
  const name = (id?: string | null) => state.players.find((p) => p.id === id)?.username ?? 'Someone';
  const who = name(e.playerId);

  switch (e.type) {
    case 'NumberAdded':
      return `${who} flips a ${e.card}.`;
    case 'ModifierAdded':
      return `${who} flips a ${e.card} modifier.`;
    case 'ActionDrawn':
      return `${who} drew ${actionLabel(e.card)}!`;
    case 'Busted':
      return `💥 ${who} BUSTS on a duplicate ${e.card}!`;
    case 'SecondChanceGained':
      return `🛡️ ${who} holds a Second Chance.`;
    case 'SecondChanceUsed':
      return `🛡️ Second Chance saves ${who} from a duplicate ${e.card}!`;
    case 'SecondChancePassed':
      return `🛡️ ${name(e.sourcePlayerId)} passes a Second Chance to ${who}.`;
    case 'SecondChanceDiscarded':
      return `🛡️ ${who} discards an extra Second Chance.`;
    case 'Frozen':
      return e.sourcePlayerId && e.sourcePlayerId !== e.playerId
        ? `❄️ ${name(e.sourcePlayerId)} freezes ${who}!`
        : `❄️ ${who} freezes — points banked.`;
    case 'FlipThreeStarted':
      return e.sourcePlayerId && e.sourcePlayerId !== e.playerId
        ? `🔄 ${name(e.sourcePlayerId)} makes ${who} Flip Three!`
        : `🔄 ${who} must Flip Three!`;
    case 'Flip7Achieved':
      return `⭐ ${who} hits FLIP 7 — +15 bonus!`;
    case 'Stayed':
      return `${who} stays.`;
    case 'RoundEnded':
      return `--- Round over — scores banked ---`;
    default:
      return null;
  }
}

function actionLabel(card?: string | null): string {
  if (card === 'Freeze') return 'a Freeze';
  if (card === 'FlipThree') return 'a Flip Three';
  if (card === 'SecondChance') return 'a Second Chance';
  return 'an action card';
}
