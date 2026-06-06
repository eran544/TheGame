namespace TheGameServer.Services.Game;

public enum PileSlot
{
    Ascending1,
    Ascending2,
    Descending1,
    Descending2
}

public enum PileDirection
{
    Ascending,
    Descending
}

public static class PileSlotExtensions
{
    public static PileDirection Direction(this PileSlot slot) => slot switch
    {
        PileSlot.Ascending1 or PileSlot.Ascending2 => PileDirection.Ascending,
        PileSlot.Descending1 or PileSlot.Descending2 => PileDirection.Descending,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}

public record PileTops(int Ascending1, int Ascending2, int Descending1, int Descending2)
{
    public static PileTops Initial() => new(
        GameRules.AscendingStartValue,
        GameRules.AscendingStartValue,
        GameRules.DescendingStartValue,
        GameRules.DescendingStartValue);

    public int GetTop(PileSlot slot) => slot switch
    {
        PileSlot.Ascending1 => Ascending1,
        PileSlot.Ascending2 => Ascending2,
        PileSlot.Descending1 => Descending1,
        PileSlot.Descending2 => Descending2,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public PileTops With(PileSlot slot, int value) => slot switch
    {
        PileSlot.Ascending1 => this with { Ascending1 = value },
        PileSlot.Ascending2 => this with { Ascending2 = value },
        PileSlot.Descending1 => this with { Descending1 = value },
        PileSlot.Descending2 => this with { Descending2 = value },
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}

public record CardPlay(int Card, PileSlot Slot);
