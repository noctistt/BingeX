# Echo development archive

Archived on 2026-10-23 at the user's request. Last complete working tree: commit `4aa5cd1` (0.14.12).

This folder preserves Echo host code, embedded original helper, tests, probes, compatibility preparation tools and development notes in their original relative layout. It is excluded from the active app project, and the Echo helper is no longer embedded in the application. No upstream runtime binaries are added here.

To inspect or run the historical integration, use a separate checkout of commit `4aa5cd1`; test project paths here are historical and are not part of current checks. To restore it, review/reapply that commit's files and integration hooks rather than copying isolated classes.

Existing user packages, engine files, settings and conversion caches are left untouched on disk. The deprecated EchoEnhancedCompatibility preference is retained for settings-file compatibility but has no active UI or engine. Local music, TagLib metadata/artwork, LibVLC playback, music controls, queues and native OpenSubsonic connections are retained.

MusicFree is a separate evaluation, not a replacement claimed ready for users.
