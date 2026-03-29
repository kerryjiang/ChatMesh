namespace ChatMesh.MauiClient.Models;

public sealed class ChatEntry
{
    public required string DisplayText { get; init; }
    public bool IsSystem { get; init; }
    public bool IsOwnMessage { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
