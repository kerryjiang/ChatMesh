namespace ChatMesh.Contract;

using System.Text.Json;

public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(MessagePayload message) =>
        JsonSerializer.Serialize(message, Options);

    public static MessagePayload? Deserialize(string json) =>
        JsonSerializer.Deserialize<MessagePayload>(json, Options);
}