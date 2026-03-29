namespace AIChatMesh.Contract;

public sealed class UserJoinedPayload : MessagePayload, IUserActionPayload
{
    public required string Username { get; set; }
}
