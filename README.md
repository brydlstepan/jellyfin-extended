# Jellyfin Extended

A Jellyfin 12 plugin that adds features Jellyfin doesn't have. Its first feature lets each device choose how it handles HDR films, so devices with wrong HDR contrast can use Jellyfin's transcoding instead. It's built for the owner's server and shared as-is, with best-effort support.

It is in early development. A device set to HDR off through the API gets HDR films transcoded. Auto and HDR on leave its requests unchanged, and there's no settings page yet. See [PLAN.md](PLAN.md) for what's coming.

## Quick start

Run the tests, then build the plugin:

```bash
dotnet test tests/Jellyfin.Plugin.Extended.Tests
dotnet build src/Jellyfin.Plugin.Extended -c Release -o artifacts/plugin
```

After building, check it against a throwaway Jellyfin 12 server with generated clips. This needs Docker and PowerShell 7:

```bash
pwsh tests/real-server/run.ps1
```

To install a build:

1. Stop Jellyfin.
2. Copy `Jellyfin.Plugin.Extended.dll` and `meta.json` from `artifacts/plugin` to `<config>/plugins/JellyfinExtended_<version>/`.
3. Start Jellyfin. The plugin shows as Active under Dashboard > Plugins.

## Requirements

- Jellyfin 12.x, with tone mapping turned on under Dashboard > Playback > Transcoding
- .NET 10 SDK, to build

## License

[GPL-3.0](LICENSE)
