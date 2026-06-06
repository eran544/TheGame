from typing import List
from game_models import CardPlay, PilesState


def _pile_top(piles: PilesState, slot: int) -> int:
    return [piles.ascending1, piles.ascending2, piles.descending1, piles.descending2][slot]


def _set_pile(piles: PilesState, slot: int, value: int) -> PilesState:
    data = piles.model_dump()
    keys = ["ascending1", "ascending2", "descending1", "descending2"]
    data[keys[slot]] = value
    return PilesState(**data)


def is_valid_play(card: int, slot: int, piles: PilesState) -> bool:
    top = _pile_top(piles, slot)
    if slot < 2:  # ascending
        return card > top or card == top - 10
    else:  # descending
        return card < top or card == top + 10


def validate_plays(plays: List[CardPlay], hand: List[int], piles: PilesState) -> bool:
    """Validate a sequence of plays: each card must be in hand and legal given running pile state."""
    available = list(hand)
    current = piles.model_copy()
    for play in plays:
        if play.card not in available:
            return False
        if not is_valid_play(play.card, play.pileSlot, current):
            return False
        available.remove(play.card)
        current = _set_pile(current, play.pileSlot, play.card)
    return True


def _play_cost(card: int, slot: int, piles: PilesState) -> int:
    """Lower cost = better move. Backwards tricks score -10."""
    top = _pile_top(piles, slot)
    if slot < 2:
        return -10 if card == top - 10 else card - top
    else:
        return -10 if card == top + 10 else top - card


# How greedily to keep playing once minCardsThisTurn is met. A play is taken
# only while the next-best cost stays BELOW this threshold:
#   safe     -> never continue past the minimum (None)
#   balanced -> continue only for cost-negative plays, i.e. backwards tricks (0)
#   risky    -> also accept small forward jumps to keep momentum (3)
_STYLE_CONTINUE_THRESHOLD = {"safe": None, "balanced": 0, "risky": 3}


def greedy_fallback(
    hand: List[int],
    piles: PilesState,
    min_cards: int,
    draw_pile_count: int,
    style: str = "balanced",
) -> List[CardPlay]:
    """
    Greedy strategy: pick the lowest-cost legal play each step.
    Meets min_cards minimum. If draw pile is empty, plays as many cards as possible.
    ``style`` tunes how many extra cards to commit once the minimum is met.
    """
    available = list(hand)
    current = piles.model_copy()
    plays: List[CardPlay] = []
    must_empty = draw_pile_count == 0
    threshold = _STYLE_CONTINUE_THRESHOLD.get(style, 0)

    while True:
        best_card, best_slot, best_cost = None, None, float("inf")
        for card in available:
            for slot in range(4):
                if is_valid_play(card, slot, current):
                    cost = _play_cost(card, slot, current)
                    if cost < best_cost:
                        best_cost, best_card, best_slot = cost, card, slot

        if best_card is None:
            break  # no legal moves left

        plays.append(CardPlay(card=best_card, pileSlot=best_slot))
        available.remove(best_card)
        current = _set_pile(current, best_slot, best_card)

        if len(plays) >= min_cards and not must_empty:
            if threshold is None:
                break  # safe: stop at the minimum
            # Continue only while the next best play stays under the threshold
            next_best_cost = float("inf")
            for card in available:
                for slot in range(4):
                    if is_valid_play(card, slot, current):
                        next_best_cost = min(next_best_cost, _play_cost(card, slot, current))
            if next_best_cost >= threshold:
                break

    return plays
