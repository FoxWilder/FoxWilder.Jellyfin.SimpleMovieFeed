## Summary

Describe the change and why it is needed.

## Related work

Link related issues without automatically closing bug reports before deployed manual acceptance.

Examples:

- Related to #123
- Addresses #123

## Validation

- [ ] GitHub Actions is expected to provide the authoritative repository build validation
- [ ] Relevant Jellyfin workflow tested when practical
- [ ] Playback/resume behavior tested when applicable
- [ ] Torrent/cache cleanup behavior tested when applicable
- [ ] Manual testing described accurately below

Describe any manual validation performed, including environment/version details that matter:

## Documentation

- [ ] Documentation updated when user-visible behavior, configuration or installation expectations changed
- [ ] No documentation claims testing or support that was not actually verified

## Security and privacy

- [ ] No credentials, access tokens, API keys, passwords, cookies, authorization headers, private data or generated runtime files containing secrets are included

## Bug-fix acceptance

For bug fixes, a successful build or merged pull request does **not** by itself mean the bug is verified fixed. Bug issues remain open until the affected behavior is manually confirmed in a deployed Jellyfin environment.

- [ ] I understand that CI green is not the same as deployed manual verification
