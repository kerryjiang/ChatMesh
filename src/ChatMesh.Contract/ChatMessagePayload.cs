namespace ChatMesh.Contract;
public sealed class ChatMessagePayload : MessagePayload
{
    public string Sender { get; set; } = string.Empty;
    public required string Content { get; set; }
    public bool Encypted { get; set; } = false;
}