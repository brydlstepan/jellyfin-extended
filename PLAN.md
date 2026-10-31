# Jellyfin Extended plan

A Jellyfin 12 plugin that adds features Jellyfin doesn't have. Its first feature lets each device choose Auto, HDR on or HDR off, so devices that show HDR films with the wrong contrast can use Jellyfin's transcoding and tone mapping instead. It's built for the owner's server and shared as-is.

## Goal

HDR films look right on the owner's devices without any device-specific workarounds, and the plugin can be installed and updated from Jellyfin's catalog. Later features join the same plugin once the first milestone ships.

## Users

- The owner: needs SDR screens and the OnePlus Nord 2 5G to stop showing over-contrasty HDR films, while HDR screens keep direct play.
- Other Jellyfin server owners: can install it from the public repository, with best-effort support.

## Scope

**In:**

- A per-device setting: Auto, HDR on, HDR off.
- Auto in the web client: use SDR when the browser reports an SDR screen.
- A toggle on the web client's playback settings page.
- An admin page that lists every device and can change its setting, for apps with no toggle.
- A plugin repository on GitHub, so Jellyfin can install and update the plugin.

**Out:**

- Detecting screens in native apps.
- Fixing Chrome's own HDR-to-SDR conversion.
- Changing media files.
- Jellyfin versions other than 12.x.
- Other features, until the first milestone ships.

## Stack and constraints

- C# on .NET 10, with JavaScript for the web client.
- Jellyfin 12.x only.
- GPL-3.0, in a public GitHub repo from the start: `brydlstepan/jellyfin-extended`.
- Needs tone mapping turned on in the server's transcoding settings.
- No film clips in the repo. Test clips are generated.

## First milestone

The full scope above, released through the plugin repository. It works when the plugin installs from the catalog on the owner's server, an SDR browser transcodes on Auto, and the phone shows correct contrast once set to HDR off.

## Decisions

| # | Decision | Answer |
|---|---|---|
| 1 | Hosting and issues | GitHub |
| 2 | How to recreate the plugin | Rebuild test-first, using the earlier plugin as reference only |
| 3 | Visibility | Public from the start |
| 4 | License | GPL-3.0 |
| 5 | Jellyfin versions | 12.x only |
| 6 | Releases | GitHub Actions on a version tag |
| 7 | Audience | The owner, shared as-is with best-effort support |
| 8 | Auto in native apps | Leave unchanged; set those devices manually |
| 9 | Automated tests | Unit tests in CI; real-server test as a local script |
| 10 | Repo creation | The agent creates it with `gh` and pushes after the kit install |
| 11 | Project shape | One plugin, Jellyfin Extended, with HDR override as its first feature |

## Open questions

- None.
