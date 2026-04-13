using Microsoft.Maui.Handlers;

namespace ChatMesh.MauiClient.Controls;

public static partial class MessageEditorConfiguration
{
    public static void Configure()
    {
        EditorHandler.Mapper.AppendToMapping(
            nameof(MessageEditor),
            (handler, view) => MapMessageEditor((EditorHandler)handler, view));
    }

    static partial void MapMessageEditor(EditorHandler handler, IEditor view);
}