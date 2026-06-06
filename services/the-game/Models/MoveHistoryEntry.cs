namespace TheGameServer.Models;

public class MoveHistoryEntry
{
    public string PlayerUsername { get; set; } = "";
    public List<MoveHistoryPlay> Plays { get; set; } = new();
}

public class MoveHistoryPlay
{
    public int Card { get; set; }
    public int PileSlot { get; set; }
}
