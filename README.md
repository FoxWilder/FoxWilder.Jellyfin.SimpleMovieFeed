# SimpleMovieFeed

[![Build](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/actions/workflows/build.yml/badge.svg)](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/actions/workflows/build.yml)
[![GitHub release](https://img.shields.io/github/v/release/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed)](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**SimpleMovieFeed** is a Jellyfin plugin that integrates an on-demand movie feed with native Jellyfin movie details, streaming, resume support, recommendations, and automatic qBittorrent lifecycle management.

The goal is to make remotely available movies behave as naturally as possible inside Jellyfin while avoiding permanent storage of downloaded movie data.

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
- Configurable startup buffering before playback.
- Automatically cleans disposable movie data after playback.
- Preserves lightweight Jellyfin identity/metadata for future resume.
- Prevents completed plugin torrents from being intentionally seeded.
- Restores posters and descriptions in Jellyfin's native details interface.
- Startup cleanup of disposable SimpleMovieFeed torrent/cache data.
- Runtime settings are configurable from the Jellyfin plugin settings page.
- qBittorrent credentials are stored separately from normal plugin configuration.

## How it works

SimpleMovieFeed combines Jellyfin Web, a Jellyfin server plugin, an external movie catalogue and a qBittorrent instance reachable by the Jellyfin server.

Selecting a movie first creates or updates a lightweight Jellyfin library entry. Jellyfin can therefore display its normal details page, poster, description, recommendations and user playback state.

The torrent is not started merely by opening the details page. Download preparation begins when the user actually chooses **Play** or **Resume**.

During playback, Jellyfin remains the playback interface. SimpleMovieFeed manages the temporary backing movie data and qBittorrent lifecycle behind the scenes.

After playback stops, disposable movie data can be removed while the lightweight .strm, poster, metadata and resume information remain available to Jellyfin.

## Requirements

- Jellyfin Server 10.11.x
- Jellyfin Web
- qBittorrent with its Web API enabled
- Windows host for protected qBittorrent credential storage

> [!IMPORTANT]
> The current release was developed and tested against Jellyfin 10.11.11 and qBittorrent 5.2.x on Windows.

## Installation

See **[Installation](docs/INSTALLATION.md)**.

Official installable packages are published through [GitHub Releases](https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases).

## Configuration

Runtime configuration is available from the SimpleMovieFeed settings page in the Jellyfin dashboard. See **[Configuration](docs/CONFIGURATION.md)** for defaults, validation ranges, restart requirements and credential-storage details.

## Architecture

For a technical overview of configuration, playback and cleanup, see **[Architecture](docs/ARCHITECTURE.md)**.

## Development

Official build and release artifacts are produced by GitHub Actions. Pull requests are validated by CI, including the .NET build and embedded JavaScript syntax.

The repository retains developer build tooling for contributors, but locally produced binaries are not the official installation or release path.

## Privacy and credentials

SimpleMovieFeed does not require credentials to be committed to the repository.

The qBittorrent credential entered on the Jellyfin settings page is stored separately from normal plugin configuration. Never commit API keys, Jellyfin access tokens, passwords, .env files, private keys or other credentials.

## Limitations

- Protected qBittorrent credential storage currently requires Windows.
- Jellyfin Web is the primary supported client.
- qBittorrent must be reachable by the Jellyfin server.
- Arbitrary torrent piece-priority control is not currently implemented; streaming relies on qBittorrent's sequential-download and first/last-piece facilities.
- Compatibility with future Jellyfin releases is not guaranteed.

## Support development

If SimpleMovieFeed is useful to you, you can support continued development through [GitHub Sponsors](https://github.com/sponsors/FoxWilder).

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
