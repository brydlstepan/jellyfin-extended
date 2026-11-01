# Jellyfin Extended

A Jellyfin 12 plugin that adds features Jellyfin doesn't have. Its first feature lets each device choose how it handles HDR films, so devices with wrong HDR contrast can use Jellyfin's transcoding instead. It's built for the owner's server and shared as-is, with best-effort support.

It is in early development. A device set to HDR off gets HDR films transcoded. On Auto, the default, a browser on an SDR screen gets the same, and other clients stay unchanged. HDR on leaves requests unchanged. An admin sets any device under Dashboard > Plugins > Jellyfin Extended, which is how apps with no toggle get set. The playback settings page has no toggle yet. See [PLAN.md](PLAN.md) for what's coming.

## Install

1. Under Dashboard > Plugins > Repositories, add this repository URL:
   `https://raw.githubusercontent.com/brydlstepan/jellyfin-extended/manifest/manifest.json`
2. Under Dashboard > Plugins > Catalog, install Jellyfin Extended.
3. Restart Jellyfin. The plugin shows as Active under Dashboard > Plugins.

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

To check installing through a plugin repository, on another throwaway server:

```bash
pwsh tests/real-server/repository.ps1
```

To install a local build by hand:

1. Stop Jellyfin.
2. Copy `Jellyfin.Plugin.Extended.dll` and `meta.json` from `artifacts/plugin` to `<config>/plugins/JellyfinExtended_<version>/`.
3. Start Jellyfin. The plugin shows as Active under Dashboard > Plugins.

## Release

Set the new version in `meta.json` and the csproj, such as `1.2.0.0`, merge that to `main`, then push the matching short tag, such as `v1.2`. The Release workflow runs the tests, creates the GitHub release with the plugin zip, and adds the version to `manifest.json` on the `manifest` branch.

## Requirements

- Jellyfin 12.x, with tone mapping turned on under Dashboard > Playback > Transcoding
- .NET 10 SDK, to build

## License

[GPL-3.0](LICENSE)
