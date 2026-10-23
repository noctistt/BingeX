# Import-time TypeScript converter

This build-only tool produces `src/BingeX/Assets/nuclear-transpiler.js` from pinned Sucrase 3.35.0 using esbuild 0.25.12. The asset is approximately 289 KiB uncompressed. It is embedded in BingeX and executed only while importing Nuclear TypeScript packages, inside the existing killable Jint worker. Neither Node, npm, esbuild executables nor node_modules are shipped.

From this directory, with Node/npm available for development:

```text
npm ci --ignore-scripts --no-audit --no-fund
npm run build
```

The lockfile pins build dependencies. esbuild's platform executable is a development tool. The converter supports TypeScript stripping and static CommonJS import conversion; it does not install dependencies, run package scripts or compile native code. The Nuclear bridge rejects unsupported runtime dependencies. The converted provider is validated and saved only after successful inspection; original provider packages are not bundled.

Bundled code comes from Sucrase, ts-interface-checker, lines-and-columns and @jridgewell mapping utilities. Package license texts are in `packaging/licenses/nuclear-transpiler-NOTICES.txt` and are copied into the installer. Keep notices synchronized if changing the pinned versions. Sources: https://github.com/alangpierce/sucrase and the repository links in the pinned npm manifests. `node_modules/` is excluded from Git.
