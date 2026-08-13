# Online Play

Operation Steel Tide currently uses a listen server: the player who selects `HOST GAME` runs the authoritative ENet session on UDP port `28960`. The included Go service stores local mission progress; it is not a gameplay server.

## Recommended free option: playit.gg

[playit.gg](https://playit.gg/) can expose the host's UDP port without router configuration. Its free plan currently supports four ports and two agents, assigns a random but static public endpoint, and does not require a credit card. Only the host runs the playit agent; joining players use the public endpoint it assigns.

1. Every player downloads the same game release and extracts it.
2. The host runs `PLAY.bat`, starts a deployment, and selects `HOST GAME`.
3. The host installs and runs the [playit agent](https://playit.gg/download), then creates a custom UDP tunnel to local address `127.0.0.1` and local port `28960`.
4. The host shares the assigned public address and port, for example `example.gl.at.ply.gg:41237`.
5. The other player runs `PLAY.bat`, chooses `JOIN GAME`, enters the complete `host:port`, and joins.

Keep both the host game and playit agent running for the whole session. This provides a public route to a player-hosted match; it is not a 24-hour dedicated server. The host may also need to allow `OperationSteelTide.exe` and the playit agent through Windows Firewall. Free accounts use automatic routing rather than a host-selected datacenter, so latency depends on player locations. Check [playit pricing](https://playit.gg/pricing) for current limits.

## Other free connection options

- **Direct port forwarding:** forward UDP `28960` on the host's router and share the public IP. This has no tunnel dependency, but requires router access and exposes the selected port.
- **Tailscale:** all players install [Tailscale](https://tailscale.com/download), join the same tailnet, and connect to the host's Tailscale IP without adding a port.
- **ZeroTier:** all players install [ZeroTier](https://www.zerotier.com/download/), join one virtual network, and connect to the host's managed IP without adding a port.

Cloud free tiers are not a drop-in solution for this build. A permanently hosted match requires a future headless dedicated-server mode; deploying only the included Go service will not host gameplay.
