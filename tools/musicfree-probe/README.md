# MusicFree research probe

Original BingeX harness, not an extension installer or security sandbox. Use reviewed plugins in a disposable process without credentials. No plugins are bundled. Dependencies are development-only and locked.

Copy package.json and package-lock.json to an isolated dependency folder and run `npm ci --ignore-scripts --no-audit --no-fund` there. Clone MusicFreePlugins and check out revision 3fa57cf37c100f32ca96e9199c6b008e79db28c1.

```
node probe.cjs PATH_TO_PLUGIN_REPO PATH_TO_DEPENDENCY_FOLDER inspect
node probe.cjs PATH_TO_PLUGIN_REPO PATH_TO_DEPENDENCY_FOLDER fixture
node probe.cjs PATH_TO_PLUGIN_REPO PATH_TO_DEPENDENCY_FOLDER live audiomack "Kevin MacLeod"
```

Inspect records exports/dependencies with HTTP blocked. Fixture runs the unmodified YouTube plugin against synthetic data. Live searches and resolves the first result, optionally checking response headers without retaining audio. Each invocation has a 55-second watchdog. Source URLs, headers and raw errors are excluded from reports. Node vm is not an untrusted-code sandbox; do not supply login variables.

See ../../docs/musicfree-evaluation.md for actual outcomes and limits.
