# Architecture

## Overview

SimpleMovieFeed is designed to preserve Jellyfin's native playback experience while sourcing movie data on demand.

The major components are:

1. **Jellyfin Web integration** - renders What's New, Search Movies and Streamable recommendations and reads the current startup-buffer setting from the runtime API.
2. **Plugin API controller** - coordinates details creation, stream preparation, playback endpoints and administrator-only configuration updates.
3. **Runtime settings** - exposes validated Jellyfin plugin configuration to runtime consumers.
4. **Movie library service** - creates lightweight `.strm` entries plus poster/NFO metadata.
5. **qBittorrent service** - adds, reuses, monitors and removes plugin-managed torrents.
6. **Torrent stream service** - exposes downloaded data to Jellyfin playback.
7. **Playback state services** - bridge on-demand movies with Jellyfin resume/history behaviour.
8. **Cleanup entry point** - removes disposable media while preserving lightweight identity state.

## Configuration architecture

Non-secret runtime settings are represented by `PluginConfiguration` and persisted through Jellyfin's plugin configuration system.

The settings page sends changes to an administrator-only SimpleMovieFeed configuration endpoint. The server validates URLs, absolute paths and numeric ranges before updating the Jellyfin plugin configuration.

`RuntimeSettings` is the common runtime access layer. It normalizes configured paths and endpoints and provides safe fallback/default values to consumers.

Cache directory, movie library directory, qBittorrent API URL and qBittorrent timeout are restart-required because relevant services capture those values during initialization. Feed/API settings, startup buffer and cleanup grace period can be consumed without that restart boundary.

The qBittorrent credential is not part of `PluginConfiguration`. On Windows it is protected with DPAPI machine scope and stored separately beneath Jellyfin's server secrets directory. Configuration/status responses expose only whether a credential is configured and whether protected storage is supported.

## Details-first lifecycle

```text
Movie card
    |
    v
Create/update lightweight Jellyfin item
    |
    v
Jellyfin native movie details
    |
    +--> More Like This
    +--> Streamable recommendations
    |
    v
User selects Play / Resume
    |
    v
Prepare qBittorrent torrent
    |
    v
Wait for configured startup buffer
    |
    v
Native Jellyfin playback
    |
    v
Playback stops
    |
    v
Cleanup disposable torrent/movie data
    |
    v
Preserve .strm + poster + NFO + resume identity
```

Opening a movie's details does **not** start its torrent.

## Resume integration

The plugin maintains enough persistent identity information for Jellyfin to represent the movie in native user interfaces such as Continue Watching.

Resume information is user-specific.

## Torrent behaviour

Plugin-managed torrents use sequential downloading and first/last-piece prioritization. Completed plugin torrents are not intentionally left seeding.

Arbitrary qBittorrent piece-priority control is not currently part of the implementation.

## Persistent versus disposable data

Persistent:

- `.strm` identity
- poster
- NFO description metadata
- catalogue/resume state required by the plugin

Disposable:

- torrent job
- downloaded movie payload
- temporary streaming/cache data
