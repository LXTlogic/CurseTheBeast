namespace CurseTheBeast.Api.Curseforge.Model;

public class MatchResult
{
    public Item[] exactMatches { get; init; } = null!;

    public class Item
    {
        public ModFile file { get; init; } = null!;
    }
}
