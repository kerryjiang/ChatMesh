# Create new projects

- there should be 2 C# (dotnet 10) projects: one chat server project and chat client project
- The server project uses SuperSocket.WebSocket to build websocket server accepting connections from chat client and communicating with it over websocket
- The client project is a Maui project which talks to the chat server over websocket. It mainly has a chat window and one setting page which can configure the server host name, username, authentication token.
- The whole process needs authentication base on token. The server side keeps the hashed token with username in configuration file, and it will uses it to validate the client connection.