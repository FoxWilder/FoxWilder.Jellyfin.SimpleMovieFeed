# Changelog

## [1.2.3] - 2026-09-09

### Fixed

- Use Jellyfin's established server address and MediaBrowser token authentication pattern for plugin configuration requests.
- Restore configuration-page loading and protected qBittorrent credential status requests in Jellyfin Web.

All notable changes to SimpleMovieFeed are documented here.

The project uses Semantic Versioning.

## [Unreleased]

## [1.2.2] - 2026-09-09

### Fixed
- Load configuration values from the plugin-owned authenticated status endpoint so settings populate correctly in Jellyfin Web.
- Save configuration without depending on Jellyfin Web's generic plugin-configuration API.

## [1.2.1] - 2026-09-09

### Fixed

- Load the SimpleMovieFeed configuration immediately when the Jellyfin configuration page initializes, while retaining the pageshow reload path.
- Restore visible populated configuration values in Jellyfin Web, including the fixed read-only RSS, movie API, and qBittorrent API endpoints.

## [1.2.0] - 2026-09-09

### Fixed

- Restored working defaults for fresh plugin deployments.
- Made the RSS feed, movie API, and qBittorrent API endpoints fixed and read-only.
- Ensured saved configurable settings are persisted through Jellyfin and read dynamically at runtime.

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

[1.2.2]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.2.2
[1.2.1]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.2.1
[1.2.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.2.0
[1.1.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.1.0
[1.0.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.0.0
