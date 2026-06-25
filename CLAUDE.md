# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Windows desktop tool for binary file structure analysis — automatically identifies and visualizes the internal layout of unknown/documented binary files through signature matching and heuristic inference, with a hex view + structure tree as the core交互 model.

**Current status**: Pre-development. Only requirements documents exist. No source code, no tech stack selected.

## Key Documents

- [docs/01-需求规格说明书.md](docs/01-需求规格说明书.md) — Full requirements spec (Chinese). Covers 10 feature modules, non-functional requirements, and version roadmap.
- [.spec/开发规范.md](.spec/开发规范.md) — Development workflow rules.

## Development Workflow Rules

From `.spec/开发规范.md`:

1. **TDD-first**: Write unit/integration tests before core logic.
2. **Plan before code**: For large changes, write a plan doc first, get confirmation, then generate code.
3. **Git before edit**: Commit before modifying files to ensure rollback capability.
4. **ASCII Art for UI**: Use ASCII art diagrams for simple UI mockups/demonstrations.
5. **DEBUG switch**: Provide a DEBUG toggle. Ship in DEBUG mode first — key info logged to files. Switch to Release mode only after tests pass.

## Version Roadmap

| Version | Focus | Key Deliverables |
|---------|-------|-----------------|
| V0.1 MVP | Core viewing & project persistence | File loading, hex view, text view, project save/open |
| V1.0 Core | Auto-recognition + manual annotation loop | Dual-engine recognition, structure tree ↔ hex linkage, manual editing, custom format rules, hex tools, live preview, bookmarks |
| V1.5 Pro | Advanced analysis | File diff (byte + field level), split views, theming, rule management, structure export (C struct/JSON) |
| V2.0 AI | Intelligent extension | LLM-powered structure recognition (optional online), batch classification |

## Architectural Concepts (from Requirements)

The requirements imply a modular architecture with these core subsystems:

### Core Modules (per 需求规格说明书 §2)

| Module | Responsibility |
|--------|---------------|
| **File Management** | Load binary files (≤200 MB), type detection, error handling |
| **Multi-View** | Hex view (offset/hex/ASCII), text view (auto-encoding detection), live decode preview, bookmarks, split panes |
| **Structure Recognition** | Dual-engine: magic-number signature matching + heuristic inference. Async for large files. Confidence scoring. AI extendable. |
| **Structure Tree + Linkage** | Bidirectional: click tree → highlight hex bytes; select hex bytes → highlight tree node |
| **Manual Editing** | CRUD on structure fields,框选 create, drag resize, undo/redo, per-field reset |
| **Custom Format Rules** | Import JSON/YAML rule libraries, conflict detection, user-defined rules override built-in |
| **File Diff** | Byte-level binary diff + field-level structural diff between two same-type files |
| **Project Management** | Save/load analysis state (.project files with hash verification) |
| **Auxiliary Tools** | Base converter (bin/dec/hex) |
| **System Config** | Theme (dark/light), layout, font, display persistence |

### Key Design Constraints

- **Offline-first**: All core features run locally. AI is an opt-in online feature.
- **Async for large files**: 200 MB files must never block the UI. Structure recognition runs async with progress feedback.
- **Performance targets**: 200 MB load ≤ 0.5s, 10 MB recognition ≤ 2s, view switch ≤ 0.3s.
- **Language**: First release is simplified Chinese only (i18n framework TBD).
- **OS**: Windows 10/11 64-bit, HiDPI support for 1080p/2K/4K.

## Tech Stack Decisions Needed

This project has NOT selected a tech stack yet. Common options for a Windows desktop binary analysis tool:

- **Rust + egui/iced** — Strong performance for binary processing, cross-platform potential, good async support
- **C++/WinRT + WinUI 3** — Native Windows, best platform integration, steep learning curve
- **C# WPF/WinUI** — Rapid development, .NET ecosystem, good for hex editor UIs
- **Electron + Rust (Tauri style)** — Web-based UI, binary processing in native Rust layer

When choosing, prioritize: memory-safe binary handling, async I/O for large files, native Windows look-and-feel, and easy debugging (DEBUG switch per development spec).

## Development Conventions

- Commit messages should reference the relevant requirements section (e.g., `§2.3` for structure recognition)
- Follow the version roadmap strictly — don't implement V1.5+ features in V0.1 scope
- When starting a new version milestone, begin with test cases per Development Rule #1
- Plan doc should cite specific sections from the requirements spec
