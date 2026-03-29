namespace ChatMesh.Contract;

public sealed class UserLeftPayload : MessagePayload, IUserActionPayload
{
    public required string Username { get; set; }
}