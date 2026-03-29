# ChatMesh – Project Guidelines

## Overview

ChatMesh is a real-time chat application with two .NET 10 projects:

- **ChatMesh.Server** – WebSocket chat server built on SuperSocket.WebSocket
- **ChatMesh.Client** – .NET MAUI client with chat UI and settings page

## Architecture

- Server accepts WebSocket connections, authenticates clients via hashed token, and routes messages between connected users.
- Client connects to the server over WebSocket. It has two pages: a **Chat** page and a **Settings** page (server hostname, username, authentication token).
- Authentication is token-based: the server stores `username → hashed_token` pairs in its configuration file and validates incoming connections against them.
- Two clients send messages to each other through the server. In the settings, there is a field called **Peer Username** which is used to identify the peer user in the chat. The client will display the peer username in the chat window when receiving messages from it. The server will route messages based on the peer username specified by the client. The messages from other users will not be delivered to the client.

## Code Style

- C# 13 / .NET 10, file-scoped namespaces, nullable reference types enabled.
- Follow standard .NET naming: `PascalCase` for public members, `_camelCase` for private fields.
- Prefer `async/await` throughout; avoid blocking calls (`Task.Result`, `.Wait()`).

## Build and Test

```bash
# Restore and build the entire solution
dotnet build ChatMesh.sln

# Run the server
dotnet run --project src/ChatMesh.Server

# Run MAUI client (desktop)
dotnet build src/ChatMesh.Client -f net10.0-maccatalyst   # macOS
dotnet build src/ChatMesh.Client -f net10.0-windows10.0.19041.0  # Windows

# Run tests
dotnet test
```

## Project Structure (planned)

```
ChatMesh.sln
src/
  ChatMesh.Server/       # SuperSocket.WebSocket server
  ChatMesh.Client/       # .NET MAUI client
tests/
  ChatMesh.Server.Tests/
```

## Key Dependencies

| Project | Package | Purpose |
|---------|---------|---------|
| Server  | SuperSocket.WebSocket | WebSocket server framework |
| Client  | Microsoft.Maui.*      | Cross-platform UI framework |

## Conventions

- **Token storage**: Server stores tokens as salted hashes (SHA-256 or bcrypt) in `appsettings.json`. Never store plaintext tokens.
- **WebSocket messages**: Use JSON-serialized message objects with a `Type` discriminator field for routing.
- **Configuration**: Use `IOptions<T>` pattern for server config; MAUI client uses `Preferences` API for persisted settings.
- **Error handling**: Log errors via `ILogger<T>`. Do not swallow exceptions silently.

## Security

- Always hash tokens before comparison; use constant-time comparison to prevent timing attacks.
- Validate and sanitize all WebSocket message payloads on the server side.
- Use TLS (wss://) in production configuration.
