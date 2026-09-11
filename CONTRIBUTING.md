# Contributing to SimpleMovieFeed

Thanks for helping improve SimpleMovieFeed. You do not need to be an experienced GitHub contributor to report a problem, suggest an improvement, or help with documentation.

## Where to go

- [README](README.md) — project overview, requirements and feature summary
- [Releases](../../releases) — official installable packages
- [Changelog](CHANGELOG.md) — release-by-release changes
- [Issues](../../issues) — reproducible bugs and tracked work
- [Discussions](../../discussions) — usage questions, configuration help and general support
- [Pull requests](../../pulls) — proposed repository changes
- [Security policy](SECURITY.md) — security-sensitive reporting guidance
- [License](LICENSE) — MIT license

## Before sharing logs or screenshots

Remove secrets and private information before posting anything publicly. If a secret may already have been exposed, rotate or revoke it before continuing.

Do not share:

- Jellyfin access tokens
- API keys
- passwords
- cookies
- authorization headers
- private server addresses when they are not needed to reproduce the problem
- personally identifying file paths, usernames or other personal information
- generated runtime data that contains credentials or private content

For a security vulnerability or accidental credential exposure, follow [SECURITY.md](SECURITY.md) instead of opening a normal public issue.

## Report a bug

Use the [bug report form](../../issues/new?template=bug_report.yml) for a reproducible problem.

A useful bug report includes:

1. SimpleMovieFeed version.
2. Jellyfin version.
3. qBittorrent version when the problem involves downloading, playback transport or cleanup.
4. Operating system and version.
5. The smallest clear sequence of steps that reproduces the problem.
6. What you expected to happen.
7. What actually happened.
8. Relevant Jellyfin logs with secrets and private information removed.
9. Relevant browser console output when the problem involves Jellyfin Web.
10. Screenshots only when they add information that is difficult to describe in text.

### Report a regression after an update

If something worked in an earlier SimpleMovieFeed release and stopped working after an update, say so explicitly. Include the last version that worked and the first version where the problem appeared, if known.

Do not downgrade or alter unrelated components only to make the report look simpler. Accurate environment details are more useful than a guessed root cause.

## Request a feature

Use the [feature request form](../../issues/new?template=feature_request.yml). Explain the problem or workflow you want to improve, the desired behavior, and any alternatives you have already considered.

## Ask a usage or configuration question

Use [GitHub Discussions](../../discussions) for questions such as:

- installation or update guidance
- configuration help
- whether a workflow is supported
- troubleshooting that is not yet a reproducible bug
- general project questions

If discussion later shows a reproducible product defect, it can be moved or restated as a bug report with the required diagnostics.

## Report a documentation problem

Use the [documentation report form](../../issues/new?template=documentation.yml) for missing, incorrect or confusing documentation. Small documentation fixes are also welcome as pull requests.

## Contribute documentation

1. Fork the repository using GitHub's **Fork** button.
2. Create a focused branch in your fork, for example `docs/improve-installation`.
3. Edit the relevant Markdown or GitHub configuration files.
4. Check links, commands, formatting and technical claims against the current repository behavior.
5. Open a pull request and explain what was unclear and how the change improves it.

Documentation changes should not claim that behavior is fixed or supported unless the repository evidence supports that statement.

## Contribute code

### 1. Fork the repository

Open the repository on GitHub and select **Fork**. Work in your fork rather than committing directly to the upstream `main` branch.

### 2. Create a branch

Create a short, descriptive branch from the current upstream `main`, for example:

```text
fix/playback-cleanup
feature/example-setting
docs/improve-troubleshooting
```

### 3. Make one focused change

Keep one primary purpose per pull request where practical. Avoid mixing unrelated refactors, formatting changes and behavior changes because that makes review and regression testing harder.

Never commit credentials, access tokens, API keys, passwords, cookies, authorization headers, private runtime data or generated files containing secrets.

### 4. Validate locally when practical

Developer builds can be useful while working:

```powershell
.\scripts\build.ps1
```

or:

```powershell
dotnet build .\src\Jellyfin.Plugin.SimpleMovieFeed\Jellyfin.Plugin.SimpleMovieFeed.csproj -c Release
```

Local builds are development evidence only. GitHub Actions is the authoritative repository build validation.

For user-visible behavior, test the relevant Jellyfin workflow when practical and describe exactly what you tested in the pull request. Update documentation when behavior, configuration or installation expectations change.

### 5. Open a pull request

Open a pull request against `main` and complete the pull request template. Include:

- what changed
- why the change is needed
- related issue numbers
- validation performed
- user-visible documentation changes, if any

Reference related bug issues without automatically closing them before deployed manual acceptance. For example, prefer `Related to #123` or `Addresses #123` while validation is pending rather than relying on automatic closure language.

## Review, testing and bug closure

A pull request merge does not by itself mean a bug is verified fixed.

For this repository:

1. GitHub Actions validates the repository build and other automated checks.
2. Review checks the implementation, scope and documentation.
3. Relevant changes may be packaged as a GitHub-built release candidate for deployed acceptance testing.
4. Bug fixes remain open until the affected behavior is manually confirmed in a deployed Jellyfin environment.
5. Only after deployed acceptance passes should the bug be treated as verified fixed and closed as completed.
6. Stable releases are published from accepted repository commits through GitHub Releases.

In short: **merged is not the same as fixed, and CI green is not the same as manually verified.**

## Pull request expectations

- Keep the change focused.
- Link related issues.
- Do not expose secrets or private generated data.
- Let GitHub Actions provide the authoritative build result.
- Describe manual validation accurately; do not claim testing that was not performed.
- Update documentation for user-visible behavior changes.
- Respond to review feedback with focused follow-up commits.
- Do not close bug issues solely because a pull request merged or CI passed.

## Releases

Official installable packages come from [GitHub Releases](../../releases). Locally produced DLLs are not official release artifacts.

A change can be merged before it appears in a stable release. For bug fixes, the project may first use an exact GitHub-built release candidate for deployed acceptance. Stable release notes should describe fixes as verified only after that acceptance has passed.
