# com.gamiable.unity-ide — IDE Editor for Unity

Forked from `com.unity.ide.rider` (v3.0.40) and adapted for multi-editor support: **VS Code, Cursor, Windsurf, Antigravity, Zed, Sublime Text, Neovim**.

Package name: `com.gamiable.unity-ide`. Host on your own UPM registry — the `com.unity.*` namespace is reserved.

## Repeatable Transformation Guide

When the upstream Rider package receives updates and you need to re-apply these changes, follow this checklist in order.

### Phase 1: Remove Rider-Only Files

These files have no equivalent and must be deleted (source + `.meta`):

| File | Why removed |
|------|-------------|
| `EditorPluginInterop.cs` | Rider EditorPlugin reflection interop |
| `RiderInitializer.cs` | Loads Rider EditorPlugin DLL from installation |
| `JetBrains.Rider.PathLocator.dll` | Native DLL for Rider path discovery |
| `StartUpMethodExecutor.cs` | JetBrains dotTrace profiler integration |
| `Util/LibcNativeInterop.cs` | `realpath()` native interop (only used for Rider path resolution) |
| `LogFileOpener.cs` | Opens Rider EditorPlugin log |
| `LoggingLevel.cs` | Rider logging level enum |
| `UnitTesting/` (entire directory) | Rider-specific reflection-based test runner bridge |

### Phase 2: Rename Rider → IDE (Content-Lite Changes)

Rename these files and update their class names + namespaces. The logic is unchanged.

| Original | Renamed To | What Changes |
|----------|-----------|--------------|
| `RiderStyles.cs` | `IDEStyles.cs` | Class name, namespace: `Packages.IDE.Editor` |
| `RiderScriptEditorDataPersisted.cs` | `IDEScriptEditorDataPersisted.cs` | Class name, namespace, `[FilePath("Library/com.gamiable.unity-ide/...")]` |
| `Util/RiderPathUtil.cs` | `Util/IDEPathUtil.cs` | Class name, namespace, `"rider-dev"` → `"code-dev"` |

### Phase 3: Adapt Files (Logic Changes)

These require both rename and logic changes:

| Original | Renamed To | Logic Changes |
|----------|-----------|---------------|
| `Util/RiderMenu.cs` | `Util/IDEMenu.cs` | Replace `RiderScriptEditor.IsRiderOrFleetInstallation` → `IDEScriptEditor.IsSupportedInstallation` |
| `PostProcessors/RiderAssetPostprocessor.cs` | `PostProcessors/IDEAssetPostprocessor.cs` | Always return `false` |
| `PluginSettings.cs` | `IDESettings.cs` | Remove Rider logging/EditorPlugin UI. Simplified to link to External Tools preferences |
| `Util/FileSystemUtil.cs` | (keep name) | Remove `GetFinalPathName()` method. Update namespace to `Packages.IDE.Editor.Util` |
| `RiderScriptEditorData.cs` | `IDEScriptEditorData.cs` | Remove `shouldLoadEditorPlugin`, `editorBuildNumber`, `prevEditorBuildNumber`. Add inline `GetEditorVersion()` using `Process.Start --version` |
| `Discovery.cs` | (keep name) | Replace RiderPathLocator/RiderFileOpener with forwarding to `IDEDiscovery.PathCallback()` |
| `RiderScriptEditor.cs` | `IDEScriptEditor.cs` | Complete rewrite — see Phase 4 |

### Phase 4: New Files

Create these from scratch:

**`IDEDiscovery.cs`** — Editor discovery replacing the native `JetBrains.Rider.PathLocator.dll`:

- Scans `PATH` + known install directories for `code`, `cursor`, `windsurf`, `antigravity`, `zed`, `subl`, `nv`
- Validates each candidate via `{path} --version`
- Platform-specific known locations:
  - macOS: `/Applications/`
  - Windows: `%LocalAppData%/Programs/`
  - Linux: `/usr/share/`, `/usr/bin/`, `/snap/bin/`, `~/.local/bin/` + PATH (all dirs scanned for all editors)
- Falls back to `EditorPrefs.GetString("kScriptsDefaultApp")` for custom paths

### Phase 5: Metadata Updates

**asmdef** (`com.unity.ide.rider.asmdef` → `com.gamiable.unity-ide.asmdef`):

```json
// Changes:
"name": "Unity.Rider.Editor"      → "Unity.IDE.Editor"
"overrideReferences": true         → false
"precompiledReferences": [         → []
  "nunit.framework.dll",
  "JetBrains.Rider.PathLocator.dll"
]
// Remove versionDefines for "com.unity.test-framework"
```

**package.json**:

```json
"name": "com.unity.ide.rider"       → "com.gamiable.unity-ide"
"displayName": "JetBrains Rider Editor" → "IDE Editor"
"version": "3.0.40"                 → "0.1.0" (or whatever is appropriate)
"dependencies": { "com.unity.ext.nunit": "1.0.6" } → {} (removed)
```

Also update `[FilePath("Library/com.gamiable.unity-ide/...")]` in `IDEScriptEditorDataPersisted.cs`.

### Phase 6: Namespace Bulk Update

Replace `Packages.Rider.Editor` → `Packages.IDE.Editor` in ALL remaining `.cs` files.

### Phase 7: Reference Cleanup

Search and replace remaining references:

| Find | Replace |
|------|---------|
| `RiderScriptEditorData.instance.*` | `IDEScriptEditorData.instance.*` |
| `RiderScriptEditorPersistedState.instance.*` | `IDEScriptEditorPersistedState.instance.*` |
| `isRiderProjectGeneration` | Remove entirely |
| `riderAssembly` (variable name) | `editorAssembly` |
| `[assembly: AssemblyTitle("Unity.Rider.Editor")]` | `[assembly: AssemblyTitle("Unity.IDE.Editor")]` |
| `[assembly: InternalsVisibleTo("Unity.Rider.EditorTests")]` | `[assembly: InternalsVisibleTo("Unity.IDE.EditorTests")]` |

### Phase 8: Editor Discovery Extension

Extend `IDEDiscovery.cs` to support additional editors:

- Added `zed`, `subl`, `nv` to editor names and display names arrays
- Added `GetMacAppName` cases for Zed, Sublime Text, Neovim
- Added known locations for macOS and Linux
- Extended `IsSupportedInstallation` to recognize new editor prefixes
- Extended `GetEditorDisplayName` for new editor display names

### Phase 9: Neovim Project File

Added `.nvproj` generation for Neovim users:

- New `GenerateNvproj()` method in `ProjectGeneration.cs`
- Called from `GenerateAndWriteSolutionAndProjects()`
- Creates `.nvproj` with `features.unity = true` and file/directory exclusions matching Unity project patterns
- `.vscode/settings.json` generation is skipped when Neovim is the active editor (`.nvproj` serves the equivalent purpose)
- Files are generated once — if they already exist, subsequent syncs leave them untouched

### Phase 10: macOS .app Bundle Validation

**Problem:** `ValidateEditor` resolved `.app` bundles to their internal binary (via `GetMacExecutablePath`) and ran `--version` on it. For Electron-based editors (Cursor, Windsurf, Antigravity), this launched the GUI instead of printing version info. `StandardOutput.ReadToEnd()` blocked until each editor was closed, causing sequential opens of all discovered editors.

**Fix:** `.app` bundles now skip `--version` validation entirely — existence in `/Applications/` is sufficient:

```csharp
if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
    return true;
```

`GetMacExecutablePath` is retained for `OpenFileInIDE` usage (where we need the internal binary for CLI arguments).

### Phase 11: Editor Name Convention

**Problem:** The macOS `.app` bundle `Neovim.app` does NOT start with the prefix `"nv"`. `"Neovim.app".StartsWith("nv")` = `false` because "Neovim" begins with "Neo", not "Nv". This caused `IsSupportedInstallation` and `GetEditorDisplayName` to miss Neovim.app when it was browsed as the external editor.

**Canonical naming:**

| Use | Search name | macOS .app name | Filename match |
|-----|-------------|-----------------|----------------|
| `EditorNames` (discovery loop) | `"nv"` | maps to `"Neovim"` via `GetMacAppName` | `"Neovim.app"` |
| `IsSupportedInstallation` prefix check | `"neovim"` then `"nv"` | `"Neovim.app".StartsWith("neovim")` = true | binary `"nv"`.`StartsWith("nv")` = true |
| `GetEditorDisplayName` prefix check | `"neovim"` then `"nv"` | returns `"Neovim"` | returns `"Neovim"` |
| Project generation guards | `"nv"` | `ExecutableStartsWithAny(path, "nv")` | matches `.app` and binary |

`GetMacAppName` maps any of these search names to the actual macOS app name:
```csharp
case "nv": return "Neovim";  // /Applications/Neovim.app
```

`FindEditorPaths` for `"nv"` searches only for `"nv"` — no `nvim` or `neovim` searches (user's Neovim is custom, not the standard distribution).

### Phase 12: File Opening Behavior

**Problem:** `OpenFileInIDE` required `line > 0` to construct a `--goto` argument. Unity sometimes passes `line = 0` when opening a file without a specific line offset, causing `hasGoto` to be false and dropping the file path entirely — the editor would open with just the project directory.

**Fix:** `hasGoto` now depends solely on whether a file path is present:

```csharp
var hasGoto = !string.IsNullOrEmpty(trimmedPath);
// before: ... && line > 0;
```

**How it works on each platform:**

| Scenario | macOS (.app bundle) | Windows/Linux (PATH binary) |
|----------|--------------------|-----------------------------|
| **Open C# Project** (no file) | `open -a "/App.app" "/projectDir"` | `executable "/projectDir"` |
| **Double-click script** (has file) | `executablePath "/projectDir" --goto "file.cs:line"` | `executable "/projectDir" --goto "file.cs:line"` |

The `--goto` flag is the standard VS Code convention, supported by all editors (the user's custom Neovim binary also accepts it).

### Phase 13: Discovery Enhancements

Changes to `IDEDiscovery.cs` beyond the initial implementation:

- **Linux known locations**: Added `~/.local/bin` (resolved from `$HOME` at runtime) — `/usr/share`, `/usr/bin`, `/snap/bin`, `~/.local/bin`
- **Stderr reading**: `ValidateEditor` consumes stderr output to prevent pipe buffer blocking (`process.StandardError.ReadToEnd()` before `WaitForExit`)
- **No special-case searches**: `FindEditorPaths` has no additional binary name lookups — each editor searches only its canonical name
- **Consistent prefix matching**: `IsSupportedInstallation` and `GetEditorDisplayName` are duplicated in both `IDEDiscovery.cs` and `IDEScriptEditor.cs` with identical prefix lists

### Phase 14: Code Quality Cleanup

- Removed commented-out dead code block in `ProjectGeneration.GetAdditionalAssets()` (folder handling inherited from upstream)
- `IDESettings.cs` keywords reordered: `"IDE"` first, editors in consistent order
- Debug logging added and later removed from `OpenFileInIDE` after successful debugging

### File Opening: How It Works

```csharp
// macOS (app bundle, no file — "Open C# Project"):
open -a "/Applications/Antigravity.app" "/project/root"

// macOS (app bundle, has file — double-click):
/Applications/Antigravity.app/Contents/MacOS/Electron "/project/root" --goto "Assets/MyScript.cs:42:10"

// Windows/Linux (PATH binary):
code "/project/root" --goto "Assets/MyScript.cs:42:10"
```

The `--goto` flag (`file:line:column`) is the VS Code convention used by all supported editors.

### Unity Version Requirement

Minimum Unity 2021.3 (same as the original Rider package). Uses `IExternalCodeEditor` interface available since Unity 2019.2.
