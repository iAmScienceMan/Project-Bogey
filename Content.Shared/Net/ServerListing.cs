namespace Content.Shared.Net;

public sealed record ServerListing
{
    public string Address { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Players { get; init; }

    public int MaxPlayers { get; init; }
}
