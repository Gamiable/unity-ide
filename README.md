# com.gamiable.unity-ide

Unity package providing multi-editor IDE integration — **VS Code, Cursor, Windsurf, Antigravity, Zed, Sublime Text, Neovim**.

Forked from `com.unity.ide.rider` (v3.0.40) and adapted for VSCode-family and other editors.

## Features

- **Editor discovery** — auto-detects VS Code, Cursor, Windsurf, Antigravity, Zed, Sublime Text, and Neovim installations on macOS and Linux (Windows: VSCode-family only)
- **Project generation** — generates `.sln` and `.csproj` files for code completion and IntelliSense
- **File opening** — opens files at the correct line and column via `--goto` from Unity
- **VSCode workspace files** — generates `.vscode/settings.json` (files.exclude for Unity assets) and `.vscode/launch.json` (Unity debug attach configurations) on first project sync
- **Neovim project file** — generates `.nvproj` for Neovim users with Unity features and file/directory exclusions

## Installation

Add to your Unity project via UPM (requires your own package registry — the `com.unity.*` namespace is reserved):

```
com.gamiable.unity-ide
```

## Requirements

- Unity 2021.3 or newer
- A supported editor installed

## Usage

Once installed, select your editor via **Unity > Preferences > External Tools > External Script Editor**.

The package will:
1. Discover your editor installation automatically
2. Generate `.sln` and `.csproj` files in the project root on each script change
3. Create `.vscode/settings.json` and `.vscode/launch.json` on first sync (preserved on subsequent runs)
4. Create `.nvproj` when using Neovim (preserved on subsequent runs)

Open C# files from Unity to launch them in your editor at the correct position.

## Maintainers

See [IMPLEMENTATION.md](./IMPLEMENTATION.md) for the repeatable transformation guide when updating from upstream Rider package.
