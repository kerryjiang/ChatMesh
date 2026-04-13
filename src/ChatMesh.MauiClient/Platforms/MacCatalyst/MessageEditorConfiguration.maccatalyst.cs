using Microsoft.Maui.Handlers;
using UIKit;

namespace ChatMesh.MauiClient.Controls;

public static partial class MessageEditorConfiguration
{
    static partial void MapMessageEditor(EditorHandler handler, IEditor view)
    {
        if (view is not MessageEditor)
            return;

        handler.PlatformView.DataDetectorTypes = UIDataDetectorType.Link;
        handler.PlatformView.Selectable = true;
        handler.PlatformView.Editable = false;
        handler.PlatformView.ScrollEnabled = false;
        handler.PlatformView.TextContainerInset = UIEdgeInsets.Zero;
        handler.PlatformView.TextContainer.LineFragmentPadding = 0;
    }
}