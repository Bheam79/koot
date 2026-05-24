namespace Koot.Api.Models;

/// <summary>
/// Static list of avatar identifiers a player can pick from when joining a game.
/// Avatars are intentionally not stored in the database — they're a fixed
/// design-time list (1..12) referenced by <see cref="GameParticipant.AvatarId"/>.
/// </summary>
public static class Avatars
{
    public const int MinId = 1;
    public const int MaxId = 12;

    public static readonly IReadOnlyList<int> All =
        Enumerable.Range(MinId, MaxId - MinId + 1).ToArray();

    public static bool IsValid(int avatarId) => avatarId >= MinId && avatarId <= MaxId;
}
