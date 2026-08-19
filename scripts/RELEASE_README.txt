OPERATION STEEL TIDE 1.3.6 - WINDOWS X64

Run PLAY.bat. Godot, .NET, and Go do not need to be installed.

DOWNLOAD SAFETY
- This is a portable ZIP, not an installer, and it does not request administrator access.
- A SHA256 checksum file is published beside the ZIP on the GitHub release page.
- Verify the ZIP before extraction if it was downloaded from any mirror.
- The prototype is not code-signed, so Windows SmartScreen may show an
  unknown-publisher warning. The complete source and packaging script are public.

MULTIPLAYER
- The host selects HOST GAME and waits in the lobby. Leave the address blank to listen on all network interfaces at UDP 28960.
- After every player has joined, the host selects START OPERATION so all peers load the same authoritative world together.
- The host may enter a local bind IP or IP:PORT when a specific interface or port is required.
- LAN players select JOIN GAME and choose the host from the automatic LAN room list.
- LAN discovery uses UDP 28961. Manual host:port entry remains available when broadcast is blocked.
- macOS players must accept the Local Network permission prompt before joining a LAN room.
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
