OPERATION STEEL TIDE 1.2.0 - WINDOWS X64

Run PLAY.bat. Godot, .NET, and Go do not need to be installed.

MULTIPLAYER
- The host selects HOST GAME. The game listens on UDP 28960.
- LAN players enter the host IP and select JOIN GAME.
- Internet players can enter host:port when the host uses port forwarding or a UDP tunnel.
- Windows Firewall may ask to allow OperationSteelTide.exe on first host launch.
- ONLINE_PLAY.md contains the free playit.gg setup and other connection options.

The included local mission service stores progression under backend\data.
Deleting that folder resets the local profile. The game still has an offline
mission fallback if the service cannot start.

This is an AI-assisted programmer-art prototype. Expect rough animation and
placeholder presentation. See the GitHub repository for source, known scope,
architecture, and third-party asset attribution:
https://github.com/AetherRadar/operation-steel-tide
