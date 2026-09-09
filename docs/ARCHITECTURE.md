# Architecture

## Overview

SimpleMovieFeed is designed to preserve Jellyfin's native playback experience while sourcing movie data on demand.

The major components are:

1. **Jellyfin Web integration** - renders What's New, Search Movies and Streamable recommendations.
2. **Plugin API controller** - coordinates details creation, stream preparation and playback endpoints.
3. **Movie library service** - creates lightweight .strm entries plus poster/NFO metadata.
4. **qBittorrent service** - adds, reuses, monitors and removes plugin-managed torrents.
5. **Torrent stream service** - exposes downloaded data to Jellyfin playback.
6. **Playback state services** - bridge on-demand movies with Jellyfin resume/history behaviour.
7. **Cleanup entry point** - removes disposable media while preserving lightweight identity state.

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
Wait for startup buffer
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

- .strm identity
- poster
- NFO description metadata
- catalogue/resume state required by the plugin

Disposable:

- torrent job
- downloaded movie payload
- temporary streaming/cache data
