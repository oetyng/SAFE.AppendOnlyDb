# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-alpha.2] - 2019-03-27
### Added
- Snapshotting functionality
- Reading from snapshot
- Snapshot tests.

### Changed
- DataTreeFactory no longer static.
- MdNodeFactory no longer static.
- Target framework set to netstandard2.0.
- MdCapacity increased from 998 to 999.
- Min lenght of payload in IImDStore method StoreImDAsync, changed from 1000 to 1.
- Snapshot of _previous_ segment stored to Metadata of _next_ segment (instead of snapshot of current segment, stored in reserved entry of current).

### Removed
- MdAccess class.
- IMdNode method: Snapshot<T>.
- MdNode reserved SNAPSHOT_KEY entry.

## [0.1.0-alpha.1] - 2019-03-22
### Added
- Migrated project files from old repo.

[Unreleased]: https://github.com/oetyng/SAFE.AppendOnlyDb/compare/v.0.1.0-alpha.2...dev-v.0.1.0-alpha.3
[0.1.0-alpha.2]: https://github.com/oetyng/SAFE.AppendOnlyDb/compare/v0.1.0-alpha.1...dev-v.0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/oetyng/SAFE.AppendOnlyDb/releases/tag/v0.1.0-alpha.1
