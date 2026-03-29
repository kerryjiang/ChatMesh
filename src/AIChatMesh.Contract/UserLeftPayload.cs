namespace AIChatMesh.Contract;

public sealed class UserLeftPayload : MessagePayload
{
    public required string Username { get; set; }
}