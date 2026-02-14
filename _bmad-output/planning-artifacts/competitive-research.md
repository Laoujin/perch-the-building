# Perch — Competitive Landscape Research

**Date:** 2026-02-14

## Closest Competitors

### chezmoi
- **URL:** https://www.chezmoi.io/
- **Comparison table:** https://www.chezmoi.io/comparison-table/
- **Stars:** 13,000+
- **Language:** Go
- **Platform:** Linux, macOS, Windows
- **Model:** Copy-on-apply (NOT symlinks) — requires `chezmoi re-add` after changes
- **Features:** Templates, encryption, password manager integration, single binary
- **Key difference from Perch:** No symlinks means changes in apps don't automatically show in git. Conflicts with Perch's "change, commit, push" philosophy.

### PSDotFiles
- **URL:** https://github.com/ralish/PSDotFiles
- **Language:** PowerShell
- **Platform:** Windows
- **Model:** Symlink-based, influenced by GNU Stow
- **Commands:** `Get-DotFiles`, `Install-DotFiles`, `Remove-DotFiles`
- **Key difference from Perch:** Closest architecturally, but appears less actively maintained. No manifest discovery, convention-over-config, or program settings management.

### Dotter
- **URL:** https://github.com/SuperCuber/dotter
- **Guide (Windows + PowerShell + Scoop):** https://medium.com/@pachoyan/mastering-dotfiles-in-windows-powershell-dotter-and-scoop-5bcf29b88b9e
- **Stars:** ~1,900
- **Language:** Rust
- **Platform:** Linux, macOS, Windows
- **Features:** Dotfile manager with templating support

### Dotbot
- **URL:** https://github.com/anishathalye/dotbot
- **Cross-platform guide:** https://brianschiller.com/blog/2024/08/05/cross-platform-dotbot/
- **Stars:** ~7,800
- **Language:** Python
- **Platform:** Cross-platform (PowerShell support)
- **Model:** YAML-configured symlinks
- **Caveat:** Symlinks to directories can be problematic on Windows

### dotfiles CLI
- **URL:** https://github.com/rhysd/dotfiles
- **Stars:** ~253
- **Language:** Go
- **Platform:** Cross-platform
- **Features:** Symlink management, dry-run support, JSON config

## Also Worth Knowing

### Mackup
- **URL:** https://github.com/lra/mackup
- **Stars:** ~15,000
- **Platform:** macOS, Linux only (NOT Windows)
- **Model:** Auto-finds settings for known apps, syncs via Dropbox/Git
- **Relevant to Perch:** Has a huge app database mapping apps to config locations — essentially Perch's scope 3 "community config path database"

### dotfiles-windows
- **URL:** https://github.com/jayharris/dotfiles-windows
- **Language:** PowerShell
- **Platform:** Windows
- **Model:** Bootstrap script that copies files to PowerShell profile folder, sets Windows defaults

## Resource Lists
- **Dotfiles utilities directory:** https://dotfiles.github.io/utilities/
- **Awesome dotfiles (curated list):** https://github.com/webpro/awesome-dotfiles

## Perch's Differentiators

1. **Symlink-based, zero re-run philosophy** — change a setting in an app, it's immediately in git. No `re-add` step (vs chezmoi)
2. **Convention-over-config discovery** — folder name = package name, auto-discovery via manifest files
3. **Program settings focus** — not just shell dotfiles but full application settings (JSON/YAML configs)
4. **Windows-native PowerShell 7+** — not a cross-platform afterthought
5. **Engine/config repo split** — open-sourceable engine with private config
6. **Scope 3 vision** — AI-assisted app discovery, registry management, MAUI UI, machine-specific overrides
