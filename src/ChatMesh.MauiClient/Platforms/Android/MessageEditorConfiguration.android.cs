using Android.Text.Method;
using Android.Text.Util;
using Microsoft.Maui.Handlers;

namespace ChatMesh.MauiClient.Controls;

public static partial class MessageEditorConfiguration
{
    static partial void MapMessageEditor(EditorHandler handler, IEditor view)
    {
        if (view is not MessageEditor)
            return;

        handler.PlatformView.AutoLinkMask = MatchOptions.WebUrls;
        handler.PlatformView.LinksClickable = true;
        handler.PlatformView.SetTextIsSelectable(true);
        handler.PlatformView.MovementMethod = LinkMovementMethod.Instance;
        handler.PlatformView.SetSelectAllOnFocus(false);
    }
}