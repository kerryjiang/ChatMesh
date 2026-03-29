namespace AIChatMesh.Contract;

public sealed class UserJoinedPayload : MessagePayload
{
    public required string Username { get; set; }
}
