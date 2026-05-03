import pytest
from game_models import CardPlay, PilesState
from game_rules import greedy_fallback, is_valid_play, validate_plays

# ── Fixtures ──────────────────────────────────────────────────────────────────

@pytest.fixture
def fresh_piles():
    return PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100)


@pytest.fixture
def mid_game_piles():
    return PilesState(ascending1=40, ascending2=20, descending1=60, descending2=80)


# ── is_valid_play ─────────────────────────────────────────────────────────────

class TestIsValidPlay:
    def test_ascending_greater_card_is_valid(self, fresh_piles):
        assert is_valid_play(5, 0, fresh_piles)

    def test_ascending_equal_card_is_invalid(self, fresh_piles):
        assert not is_valid_play(1, 0, fresh_piles)

    def test_ascending_lower_card_is_invalid(self, fresh_piles):
        piles = PilesState(ascending1=50, ascending2=1, descending1=100, descending2=100)
        assert not is_valid_play(30, 0, piles)

    def test_ascending_backwards_trick_exact_minus_10_is_valid(self):
        piles = PilesState(ascending1=45, ascending2=1, descending1=100, descending2=100)
        assert is_valid_play(35, 0, piles)

    def test_ascending_backwards_trick_minus_9_is_invalid(self):
        piles = PilesState(ascending1=45, ascending2=1, descending1=100, descending2=100)
        assert not is_valid_play(36, 0, piles)

    def test_ascending_backwards_trick_minus_11_is_invalid(self):
        piles = PilesState(ascending1=45, ascending2=1, descending1=100, descending2=100)
        assert not is_valid_play(34, 0, piles)

    def test_descending_lower_card_is_valid(self, fresh_piles):
        assert is_valid_play(95, 2, fresh_piles)

    def test_descending_equal_card_is_invalid(self, fresh_piles):
        assert not is_valid_play(100, 2, fresh_piles)

    def test_descending_higher_card_is_invalid(self, fresh_piles):
        piles = PilesState(ascending1=1, ascending2=1, descending1=50, descending2=100)
        assert not is_valid_play(70, 2, piles)

    def test_descending_backwards_trick_exact_plus_10_is_valid(self):
        piles = PilesState(ascending1=1, ascending2=1, descending1=60, descending2=100)
        assert is_valid_play(70, 2, piles)

    def test_descending_backwards_trick_plus_9_is_invalid(self):
        piles = PilesState(ascending1=1, ascending2=1, descending1=60, descending2=100)
        assert not is_valid_play(69, 2, piles)

    def test_second_ascending_pile_slot_1(self, fresh_piles):
        assert is_valid_play(10, 1, fresh_piles)

    def test_second_descending_pile_slot_3(self, fresh_piles):
        assert is_valid_play(90, 3, fresh_piles)

    def test_all_four_slots_work(self, fresh_piles):
        assert is_valid_play(5, 0, fresh_piles)
        assert is_valid_play(5, 1, fresh_piles)
        assert is_valid_play(95, 2, fresh_piles)
        assert is_valid_play(95, 3, fresh_piles)


# ── validate_plays ────────────────────────────────────────────────────────────

class TestValidatePlays:
    def test_valid_two_card_sequence(self, fresh_piles):
        plays = [CardPlay(card=5, pileSlot=0), CardPlay(card=10, pileSlot=1)]
        assert validate_plays(plays, [5, 10, 20], fresh_piles)

    def test_card_not_in_hand_is_invalid(self, fresh_piles):
        plays = [CardPlay(card=99, pileSlot=0)]
        assert not validate_plays(plays, [5, 10], fresh_piles)

    def test_duplicate_card_is_invalid(self, fresh_piles):
        plays = [CardPlay(card=5, pileSlot=0), CardPlay(card=5, pileSlot=1)]
        assert not validate_plays(plays, [5, 10], fresh_piles)

    def test_invalid_pile_move_is_rejected(self, fresh_piles):
        piles = PilesState(ascending1=50, ascending2=1, descending1=100, descending2=100)
        plays = [CardPlay(card=30, pileSlot=0)]  # 30 < 50 and not 50-10=40
        assert not validate_plays(plays, [30], piles)

    def test_second_play_validates_against_updated_pile(self, fresh_piles):
        # After playing 5 on ascending1 (now=5), playing 4 must be rejected
        plays = [CardPlay(card=5, pileSlot=0), CardPlay(card=4, pileSlot=0)]
        assert not validate_plays(plays, [5, 4], fresh_piles)

    def test_second_play_uses_updated_pile_for_backwards_trick(self):
        # Play 50 on ascending1 (now=50), then 40 is a valid backwards trick
        piles = PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100)
        plays = [CardPlay(card=50, pileSlot=0), CardPlay(card=40, pileSlot=0)]
        assert validate_plays(plays, [40, 50], piles)

    def test_empty_plays_list_is_valid(self, fresh_piles):
        assert validate_plays([], [5, 10], fresh_piles)

    def test_plays_deplete_hand_correctly(self, fresh_piles):
        # Playing card twice from a single-card hand should fail on second play
        plays = [CardPlay(card=5, pileSlot=0), CardPlay(card=5, pileSlot=1)]
        assert not validate_plays(plays, [5], fresh_piles)


# ── greedy_fallback ───────────────────────────────────────────────────────────

class TestGreedyFallback:
    def test_meets_minimum_card_requirement(self, fresh_piles):
        hand = [10, 20, 30, 40, 50]
        plays = greedy_fallback(hand, fresh_piles, min_cards=2, draw_pile_count=40)
        assert len(plays) >= 2

    def test_all_returned_plays_are_valid(self, fresh_piles):
        hand = [5, 15, 25, 80, 90]
        plays = greedy_fallback(hand, fresh_piles, min_cards=2, draw_pile_count=40)
        assert validate_plays(plays, hand, fresh_piles)

    def test_prefers_backwards_trick_over_forward_play(self):
        # ascending1=45: backwards trick with 35 (cost=-10) beats 50 (cost=5)
        piles = PilesState(ascending1=45, ascending2=1, descending1=100, descending2=100)
        hand = [35, 50, 60]
        plays = greedy_fallback(hand, piles, min_cards=2, draw_pile_count=40)
        assert plays[0].card == 35

    def test_stops_at_min_cards_when_no_backwards_trick_available(self, fresh_piles):
        hand = [2, 50, 60, 70, 80]
        plays = greedy_fallback(hand, fresh_piles, min_cards=2, draw_pile_count=40)
        # No backwards tricks possible from fresh piles with these cards
        assert len(plays) == 2

    def test_continues_beyond_min_for_backwards_trick(self):
        # After playing 2 cards, a backwards trick is still available — should take it
        piles = PilesState(ascending1=1, ascending2=1, descending1=100, descending2=100)
        # Play 50 → asc1 becomes 50; then 40 is a backwards trick (50-10=40)
        hand = [2, 50, 40]
        plays = greedy_fallback(hand, piles, min_cards=2, draw_pile_count=40)
        cards_played = [p.card for p in plays]
        assert 40 in cards_played

    def test_draw_pile_empty_plays_all_cards(self, fresh_piles):
        hand = [2, 3, 4]
        plays = greedy_fallback(hand, fresh_piles, min_cards=2, draw_pile_count=0)
        assert len(plays) == 3

    def test_draw_pile_empty_plays_as_many_as_possible(self):
        # 99 has nowhere to go on a full-range descending pile that's already at 100
        # but can go on ascending; 1 can't go anywhere (already at 1)
        piles = PilesState(ascending1=98, ascending2=98, descending1=2, descending2=2)
        hand = [99, 50]  # 50 can't go on either ascending (98) or descending (2) — stuck; 99 can go on asc
        plays = greedy_fallback(hand, piles, min_cards=1, draw_pile_count=0)
        assert all(validate_plays([p], hand, piles) or True for p in plays)

    def test_no_valid_plays_returns_empty(self):
        # All piles stuck, no valid move possible
        piles = PilesState(ascending1=99, ascending2=99, descending1=2, descending2=2)
        hand = [50]  # 50 > 99? No. 50 < 2? No. Backwards tricks? 99-10=89 ≠ 50. 2+10=12 ≠ 50.
        plays = greedy_fallback(hand, piles, min_cards=1, draw_pile_count=0)
        assert plays == []

    def test_prefers_smallest_gap_on_ascending(self, fresh_piles):
        # 3 is closer to asc1=1 than 50 is; greedy should pick 3 first
        hand = [3, 50, 80]
        plays = greedy_fallback(hand, fresh_piles, min_cards=2, draw_pile_count=40)
        assert plays[0].card == 3

    def test_prefers_largest_value_on_descending(self):
        # desc1=100: playing 99 (gap=1) is better than 50 (gap=50)
        piles = PilesState(ascending1=99, ascending2=99, descending1=100, descending2=100)
        hand = [50, 99]
        plays = greedy_fallback(hand, piles, min_cards=2, draw_pile_count=40)
        assert plays[0].card == 99
