# SimpleMovieFeed

[![Build](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/actions/workflows/build.yml/badge.svg)](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/actions/workflows/build.yml)
[![GitHub release](https://img.shields.io/github/v/release/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed)](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**SimpleMovieFeed** is a Jellyfin plugin that integrates an on-demand movie feed with native Jellyfin movie details, streaming, resume support, recommendations, and automatic qBittorrent lifecycle management.

The goal is to make remotely available movies behave as naturally as possible inside Jellyfin while avoiding permanent storage of the downloaded movie data.

## Features

- **What's New** movie feed integrated into Jellyfin Web.
- Search an external movie catalogue directly from Jellyfin.
- Opens movies in Jellyfin's native movie-details interface before playback.
- Native Jellyfin Play and Resume workflow.
- Native **Continue Watching** integration.
- Per-user playback/resume state.
- Native **More Like This** recommendations remain available.
- Additional **Streamable recommendations** section.
- Automatically selects the highest available stream quality.
- Progressive playback through qBittorrent.
- Uses sequential downloading and first/last-piece prioritization.
- Waits for a startup buffer before playback.
- Automatically cleans disposable movie data after playback.
- Preserves lightweight Jellyfin identity/metadata for future resume.
- Prevents completed plugin torrents from being intentionally seeded.
- Restores posters and descriptions in Jellyfin's native details interface.
- Startup cleanup of disposable SimpleMovieFeed torrent/cache data.

## How it works

SimpleMovieFeed combines Jellyfin Web, a Jellyfin server plugin, an external movie catalogue and a local qBittorrent instance.

Selecting a movie first creates or updates a lightweight Jellyfin library entry. Jellyfin can therefore display its normal details page, poster, description, recommendations and user playback state.

The torrent is not started merely by opening the details page. Download preparation begins when the user actually chooses **Play** or **Resume**.

During playback, Jellyfin remains the playback interface. SimpleMovieFeed manages the temporary backing movie data and qBittorrent lifecycle behind the scenes.

After playback stops, disposable movie data can be removed while the lightweight .strm, poster, metadata and resume information remain available to Jellyfin.

## Requirements

- Jellyfin Server 10.11.x
- Jellyfin Web
- .NET 9 SDK for building from source
- qBittorrent with its Web API enabled
- Windows host for the current implementation

> [!IMPORTANT]
> The current release was developed and tested against Jellyfin 10.11.11 and qBittorrent 5.2.x on Windows.

## Installation

See **[Installation](docs/INSTALLATION.md)**.

Release packages are available from the [GitHub Releases](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases) page.

## Configuration

See **[Configuration](docs/CONFIGURATION.md)** for the current qBittorrent and storage requirements.

## Architecture

For a technical overview of the playback and cleanup lifecycle, see **[Architecture](docs/ARCHITECTURE.md)**.

## Building from source

```powershell
git clone https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed.git
cd FoxWilder.Jellyfin.SimpleMovieFeed
.\scripts\build.ps1
```

Or build the project directly:

```powershell
dotnet build .\src\Jellyfin.Plugin.SimpleMovieFeed\Jellyfin.Plugin.SimpleMovieFeed.csproj -c Release
```

## Privacy and credentials

SimpleMovieFeed does not require credentials to be committed to the repository.

qBittorrent authentication material must remain outside the source tree. Never commit API keys, Jellyfin access tokens, passwords, .env files, private keys or other credentials.

## Limitations

- The current implementation is Windows-oriented.
- Jellyfin Web is the primary supported client.
- qBittorrent must be reachable by the Jellyfin server.
- Arbitrary torrent piece-priority control is not currently implemented; streaming relies on qBittorrent's sequential-download and first/last-piece facilities.
- Compatibility with future Jellyfin releases is not guaranteed.

## Legal notice

SimpleMovieFeed is an independent project and is not affiliated with, endorsed by, or sponsored by Jellyfin, qBittorrent, or any external catalogue/provider.

Users are responsible for ensuring that their use of the software and any content accessed through it complies with applicable laws, licences, service terms and copyright requirements.

No copyrighted movie content is distributed with this repository or its releases.

## Contributing

Bug reports and contributions are welcome. See **[CONTRIBUTING.md](CONTRIBUTING.md)**.

Please report security-sensitive issues according to **[SECURITY.md](SECURITY.md)** rather than through a public issue.

## License

Copyright (c) 2026 FoxWilder

Released under the **MIT License**. See [LICENSE](LICENSE).
