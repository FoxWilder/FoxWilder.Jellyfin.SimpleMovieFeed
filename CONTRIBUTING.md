# Contributing to SimpleMovieFeed

Thanks for your interest in improving SimpleMovieFeed.

## Reporting bugs

Use the GitHub bug-report form and include:

- Jellyfin version
- qBittorrent version
- Windows version
- SimpleMovieFeed version
- reproduction steps
- expected behaviour
- actual behaviour
- relevant logs with all tokens, API keys and personal information removed

## Feature requests

Use the feature-request form and explain the use case as well as the desired behaviour.

## Pull requests

1. Fork the repository.
2. Create a feature branch.
3. Keep changes focused.
4. Build the Release configuration.
5. Test the relevant Jellyfin workflow.
6. Update documentation when behaviour changes.
7. Open a pull request.

## Build

```powershell
.\scripts\build.ps1
```

or:

```powershell
dotnet build .\src\Jellyfin.Plugin.SimpleMovieFeed\Jellyfin.Plugin.SimpleMovieFeed.csproj -c Release
```

## Security

Never include access tokens, API keys, passwords, private movie data or other secrets in issues, commits or pull requests.
