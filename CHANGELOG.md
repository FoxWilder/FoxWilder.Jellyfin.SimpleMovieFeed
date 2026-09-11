# Changelog


All notable changes to SimpleMovieFeed are documented here.

The project uses Semantic Versioning.

## [Unreleased]

## [1.2.8] - 2026-09-11

### Added

- Add a GitHub-built release-candidate workflow so exact candidate artifacts can be deployed and accepted before a stable release is published.
- Add playback lifecycle diagnostics for startup activation, stop correlation, deferred cleanup, and final cleanup.

### Changed

- Remove completed plugin-managed torrents from qBittorrent without deleting retained playback files.
- Retain only plugin-validated completed cache entries and allow later playback to reuse the exact retained file after qBittorrent registration has been removed.
- Prefer Jellyfin session identity for playback correlation, with play-session identity used only as a fallback.
- Serialize final torrent/cache cleanup, re-check active usage before deletion, and retry transient file-deletion failures.

### Fixed

- Prevent final cleanup from deleting or orphaning cache data while another playback/startup consumer still uses the same torrent.
- Prevent same-user/same-media startup cancellation from removing a separate active or prepared startup.
- Restore playback startup from validated completed cache after the corresponding completed torrent has already been removed from qBittorrent.
- Ensure retained cache is removed after the final playback consumer ends and the cleanup grace period expires.

## [1.2.7] - 2026-09-10

### Fixed

- Track active playback by Jellyfin play session so concurrent playback of the same item does not overwrite another session's state.
- Preserve separate prepared startup records for concurrent users and promote the matching record when playback starts.
- Prevent one playback session from deleting torrent data while another session still uses the same torrent.
- Keep playback progress, resume updates, Continue Watching state, and watch history scoped to the correct Jellyfin user/session.

## [1.2.6] - 2026-09-10

### Fixed

* Cancel pending playback startup work when the initiating playback request is abandoned.
* Isolate prepared playback state so one playback attempt cannot consume or remove another attempt's startup state.
* Prevent cleanup from removing a torrent while another active or pending playback startup still uses it.
## [1.2.5] - 2026-09-10

### Fixed

- Load the SimpleMovieFeed configuration controller from the global plugin lifecycle instead of relying on the configuration page to execute embedded JavaScript.
- Ensure configuration loading and saving initialize correctly after Jellyfin Web navigation events.


## [1.2.4] - 2026-09-10

### Fixed

- Run the SimpleMovieFeed configuration-page controller from a dedicated embedded JavaScript resource so Jellyfin Web can execute configuration loading and saving logic.

## [1.2.3] - 2026-09-09

### Fixed

- Use Jellyfin's established server address and MediaBrowser token authentication pattern for plugin configuration requests.
- Restore configuration-page loading and protected qBittorrent credential status requests in Jellyfin Web.

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

[1.2.8]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.2.8
[1.2.2]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.2.2
[1.2.1]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.2.1
[1.2.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.2.0
[1.1.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.1.0
[1.0.0]: https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed/releases/tag/v1.0.0
