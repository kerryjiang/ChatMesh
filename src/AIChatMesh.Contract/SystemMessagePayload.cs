namespace AIChatMesh.Contract;

public sealed class SystemMessagePayload : MessagePayload
{
    public required string Content { get; set; }
}