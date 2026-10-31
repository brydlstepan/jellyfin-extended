# Jellyfin Extended

A Jellyfin 12 plugin that will let each device choose Auto, HDR on or HDR off, so devices with wrong HDR contrast can use Jellyfin's transcoding instead. It's built for the owner's server and shared as-is, with best-effort support.

It is in early development and doesn't change playback yet. See [PLAN.md](PLAN.md) for what's coming.

## Quick start

Run the tests, then build the plugin:

```bash
dotnet test tests/Jellyfin.Plugin.Extended.Tests
dotnet build src/Jellyfin.Plugin.Extended -c Release -o artifacts/plugin
```

To install a build:

1. Stop Jellyfin.
2. Copy `Jellyfin.Plugin.Extended.dll` and `meta.json` from `artifacts/plugin` to `<config>/plugins/JellyfinExtended_<version>/`.
3. Start Jellyfin. The plugin shows as Active under Dashboard > Plugins.

## Requirements

- Jellyfin 12.x
- .NET 10 SDK, to build

## License

[GPL-3.0](LICENSE)
