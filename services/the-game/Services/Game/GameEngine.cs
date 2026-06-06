namespace TheGameServer.Services.Game;

public interface IGameEngine
{
    MoveValidationResult ValidateMove(int card, PileSlot slot, int pileTopValue);
    TurnValidationResult ValidateTurn(IList<CardPlay> plays, IList<int> hand, PileTops piles, int minCards);
    bool CanPlayMinimumCards(IList<int> hand, PileTops piles, int minCards);
    GameScore CalculateScore(int cardsRemaining);
}

public record MoveValidationResult(bool IsValid, bool IsBackwardsTrick, string? Error)
{
    public static MoveValidationResult Ok(bool isBackwardsTrick) => new(true, isBackwardsTrick, null);
    public static MoveValidationResult Fail(string error) => new(false, false, error);
}

public record TurnValidationResult(bool IsValid, PileTops ResultingPiles, IList<int> ResultingHand, string? Error)
{
    public static TurnValidationResult Fail(string error) => new(false, PileTops.Initial(), Array.Empty<int>(), error);
}

public enum GameRating { Perfect, Excellent, TryAgain }

public record GameScore(int CardsRemaining, GameRating Rating, bool IsPerfectGame);

public class GameEngine : IGameEngine
{
    public MoveValidationResult ValidateMove(int card, PileSlot slot, int pileTopValue)
    {
        if (card < CardDeck.MinCardValue || card > CardDeck.MaxCardValue)
            return MoveValidationResult.Fail(
                $"Card value must be between {CardDeck.MinCardValue} and {CardDeck.MaxCardValue}");

        return slot.Direction() switch
        {
            PileDirection.Ascending => ValidateAscending(card, pileTopValue),
            PileDirection.Descending => ValidateDescending(card, pileTopValue),
            _ => MoveValidationResult.Fail("Unknown pile direction")
        };
    }

    public TurnValidationResult ValidateTurn(IList<CardPlay> plays, IList<int> hand, PileTops piles, int minCards)
    {
        if (plays.Count < minCards)
            return TurnValidationResult.Fail($"Must play at least {minCards} card(s) this turn");

        var workingHand = hand.ToList();
        var workingPiles = piles;

        foreach (var play in plays)
        {
            var indexInHand = workingHand.IndexOf(play.Card);
            if (indexInHand < 0)
                return TurnValidationResult.Fail($"Card {play.Card} is not in hand");

            var top = workingPiles.GetTop(play.Slot);
            var validation = ValidateMove(play.Card, play.Slot, top);
            if (!validation.IsValid)
                return TurnValidationResult.Fail(validation.Error!);

            workingHand.RemoveAt(indexInHand);
            workingPiles = workingPiles.With(play.Slot, play.Card);
        }

        return new TurnValidationResult(true, workingPiles, workingHand, null);
    }

    public bool CanPlayMinimumCards(IList<int> hand, PileTops piles, int minCards)
    {
        if (minCards <= 0) return true;
        if (hand.Count < minCards) return false;
        return TryPlayCardsRecursive(hand.ToList(), piles, minCards);
    }

    public GameScore CalculateScore(int cardsRemaining)
    {
        if (cardsRemaining < 0)
            throw new ArgumentOutOfRangeException(nameof(cardsRemaining), "Cards remaining cannot be negative");

        return cardsRemaining switch
        {
            0 => new GameScore(0, GameRating.Perfect, true),
            < 10 => new GameScore(cardsRemaining, GameRating.Excellent, false),
            _ => new GameScore(cardsRemaining, GameRating.TryAgain, false)
        };
    }

    private static MoveValidationResult ValidateAscending(int card, int top)
    {
        if (card == top - GameRules.BackwardsTrickDelta)
            return MoveValidationResult.Ok(isBackwardsTrick: true);
        if (card > top)
            return MoveValidationResult.Ok(isBackwardsTrick: false);
        return MoveValidationResult.Fail($"Card {card} cannot be played on ascending pile (top: {top})");
    }

    private static MoveValidationResult ValidateDescending(int card, int top)
    {
        if (card == top + GameRules.BackwardsTrickDelta)
            return MoveValidationResult.Ok(isBackwardsTrick: true);
        if (card < top)
            return MoveValidationResult.Ok(isBackwardsTrick: false);
        return MoveValidationResult.Fail($"Card {card} cannot be played on descending pile (top: {top})");
    }

    private bool TryPlayCardsRecursive(List<int> hand, PileTops piles, int remaining)
    {
        if (remaining == 0) return true;

        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            foreach (var slot in Enum.GetValues<PileSlot>())
            {
                var validation = ValidateMove(card, slot, piles.GetTop(slot));
                if (!validation.IsValid) continue;

                hand.RemoveAt(i);
                var nextPiles = piles.With(slot, card);
                if (TryPlayCardsRecursive(hand, nextPiles, remaining - 1))
                {
                    hand.Insert(i, card);
                    return true;
                }
                hand.Insert(i, card);
            }
        }
        return false;
    }
}
