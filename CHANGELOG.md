# Changelog

All notable changes to SimpleMovieFeed are documented here.

The project uses Semantic Versioning.

## [Unreleased]

## [1.1.0] - 2026-09-09

### Added

- Jellyfin dashboard settings for feed/API endpoints, cache/library directories, qBittorrent endpoint, startup buffer, cleanup grace period and qBittorrent timeout.
- Protected write-only qBittorrent credential configuration on Windows using DPAPI machine-scope storage outside normal plugin configuration.
- Administrator-only configuration status and validated configuration update endpoints.
- Runtime startup-buffer synchronization for Jellyfin Web.
- JavaScript syntax validation in GitHub Actions.

### Changed

- Runtime services now consume centralized configurable settings instead of fixed operational values.
- Configuration changes that affect initialized paths or qBittorrent connectivity are explicitly reported as restart-required.

## [1.0.0] - 2026-09-09

### Added

- Initial public release.
- What's New feed.
- Movie catalogue search.
- Native Jellyfin movie-details integration.
- Native Play and Resume workflow.
- Jellyfin Continue Watching support.
- Per-user resume state.
- YTS-backed streamable recommendations.
- Native More Like This compatibility.
- Highest-quality automatic stream selection.
- qBittorrent-backed progressive playback.
- 256 MiB startup buffering.
- Sequential torrent downloading.
- Automatic torrent and temporary-media cleanup.
- Playback-stop cleanup grace period.
- Persistent lightweight .strm identity entries.
- Poster and movie-description persistence.
- Jellyfin library-scan integration for newly created entries.
- Startup cleanup of disposable SimpleMovieFeed data.
- Consistent Jellyfin Web card and section presentation.

[1.1.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.1.0
[1.0.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.0.0
