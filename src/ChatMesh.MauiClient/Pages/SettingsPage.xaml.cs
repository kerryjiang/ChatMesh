namespace ChatMesh.MauiClient.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        ServerHostEntry.Text = Preferences.Default.Get("ServerHost", "localhost:4040");
        UsernameEntry.Text = Preferences.Default.Get("Username", string.Empty);
        TokenEntry.Text = Preferences.Default.Get("AuthToken", string.Empty);
        PeerUsernameEntry.Text = Preferences.Default.Get("PeerUsername", string.Empty);
        MessageEncryptionKeyEntry.Text = Preferences.Default.Get("MessageEncryptionKey", string.Empty);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var host = ServerHostEntry.Text?.Trim() ?? "localhost:4040";
        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var token = TokenEntry.Text ?? string.Empty;
        var peerUsername = PeerUsernameEntry.Text?.Trim() ?? string.Empty;
        var messageEncryptionKey = MessageEncryptionKeyEntry.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            await DisplayAlertAsync("Validation", "Username is required.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync("Validation", "Authentication token is required.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(peerUsername))
        {
            await DisplayAlertAsync("Validation", "Peer username is required.", "OK");
            return;
        }

        Preferences.Default.Set("ServerHost", host);
        Preferences.Default.Set("Username", username);
        Preferences.Default.Set("AuthToken", token);
        Preferences.Default.Set("PeerUsername", peerUsername);
        Preferences.Default.Set("MessageEncryptionKey", messageEncryptionKey);

        SavedLabel.Text = "Settings saved!";
        SavedLabel.IsVisible = true;

        await Task.Delay(2000);
        SavedLabel.IsVisible = false;
    }
}
