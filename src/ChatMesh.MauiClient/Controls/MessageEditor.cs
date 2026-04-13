namespace ChatMesh.MauiClient.Controls;

public sealed class MessageEditor : Editor
{
    public MessageEditor()
    {
        IsReadOnly = true;
        AutoSize = EditorAutoSizeOption.TextChanges;
        BackgroundColor = Colors.Transparent;
        IsSpellCheckEnabled = false;
        IsTextPredictionEnabled = false;
    }
}