# AIChatMesh.Client – UI Guidelines

# Layout

- There is a menu bar on the top. It should have different background color with the chat message panel.
_ There is another Status bar under the menu bar with different background color.


# The Menu Bar

- If the settings are filled fully, the app should connect to the server automatically when it starts.
- If the client is not connected, show two menu items: Connect and Settings on the right.
- If the client is connected, show menu items Disconnect, Settings on the right.
- All menu items should have image plus text to make them look better and understandable.

# The Status Bar.

- The staus bar only shows StatusLabel. The text of status label if the client is connected is a string in the format "${Username}/${PeerUsername}".