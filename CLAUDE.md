# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Windows desktop tool for binary file structure analysis — automatically identifies and visualizes the internal layout of unknown binary files through signature matching and heuristic inference, with a hex view + structure tree as the core interaction model.

**Current status**: V1.7.1. C# .NET 10 WPF, with 50+ predefined format rules, ZIP/RAR4/RAR5/CAB/PAK/7z/TAR/GZip container expansion, multi-volume ZIP/7z lazy-loading, PDF/MP4/MKV/JPG/EPUB/MOBI/AZW3 format recognition, PE packer detection (20+), structure tree management, 7z LZMA compressed header expansion, 213 xUnit tests passing (0 warnings, 0 errors).

## Tech Stack

| Component | Choice |
|-----------|--------|
| Language / Runtime | C# .NET 10 |
| UI Framework | WPF (CommunityToolkit.Mvvm) |
| DI | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog + Sinks.File + Sinks.Debug |
| Serialization | System.Text.Json |
| YAML Import (V1.0) | YamlDotNet |
| Testing | xUnit + Moq |

## Build & Test Commands

```bash
# Build all projects
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/FileStruct.Core.Tests

# Run specific test
dotnet test --filter "BinaryBufferTests.LoadFromFile_ValidFile_ReturnsBuffer"
```

## Project Structure

```
FileStruct.sln
├── src/
│   ├── FileStruct.Core/          # Pure domain models (no UI deps)
│   │   ├── Models/               # BinaryBuffer, StructureNode, ProjectFile...
│   │   ├── Interfaces/           # IFileService, IStructureRecognizer...
│   │   └── Exceptions/           # FileLoadException, FileTooLargeException...
│   ├── FileStruct.Infrastructure/ # I/O, logging, config persistence
│   │   ├── Logging/              # LogService (Serilog wrapper)
│   │   └── Configuration/        # ConfigFileStore
│   ├── FileStruct.Services/      # Business logic implementations
│   │   ├── FileManagement/       # FileService, FileTypeDetector
│   │   ├── ProjectManagement/    # ProjectService, ProjectSerializer
│   │   └── Configuration/        # ConfigService
│   └── FileStruct.App/           # WPF application (entry point)
│       ├── App.xaml(.cs)         # DI setup in OnStartup
│       ├── Views/                # MainWindow, HexEditorView, TextView
│       ├── ViewModels/           # MainViewModel, HexEditorViewModel, TextViewModel
│       ├── Controls/             # HexView (virtualizing custom control)
│       ├── Converters/           # BoolToVisibility, FileSize converters
│       └── Styles/               # Generic.xaml (HexView default template)
├── tests/
│   ├── FileStruct.Core.Tests/    # BinaryBuffer、StructureNode 单元/回归测试
│   ├── FileStruct.Services.Tests/ # 服务层单元测试 + UI 控件回归测试
│   ├── FileStruct.Integration.Tests/ # End-to-end project save/load
│   └── FileStruct.Performance.Tests/ # Performance benchmarks (V1.0+)
├── samples/                      # Test binary files
└── tools/                        # Test data generation scripts
```

## Key Documents

- [docs/01-需求规格说明书.md](docs/01-需求规格说明书.md) — Full requirements spec (Chinese). 10 feature modules, non-functional requirements, version roadmap.
- [.spec/开发规范.md](.spec/开发规范.md) — Development workflow rules.
- [C:\Users\tiger\.claude\plans](C:\Users\tiger\.claude\plans) — Implementation plans.

## Core Architecture

### Layered Architecture
```
Presentation (WPF Views) → ViewModels → Services (Business Logic) → Infrastructure (I/O/Logging) → Core (Models/Interfaces)
```

### Communication Pattern
- View → ViewModel: Data binding + Commands (CommunityToolkit.Mvvm source generators)
- ViewModel → Service: Interface injection via DI constructor
- Cross-cutting events: WeakReferenceMessenger (CommunityToolkit.Mvvm)
- Key messages: FileLoadedMessage, StructureRecognizedMessage, SelectionChangedMessage

### Key Data Flow: File Load -> Display
```
User opens file → MainViewModel.OpenFileCommand
  → IFileService.LoadFileAsync(path)         // async via MemoryMappedFile
    → FileTypeDetector.Detect(path)           // magic + extension matching
    → BinaryBuffer (wraps MMF, zero-copy)
  → HexEditorViewModel.Buffer = buffer        // triggers hex view render
  → FileType.IsText? TextView : HexView       // default view selection
```

## Development Workflow Rules

From `.spec/开发规范.md`:

1. **TDD-first**: Write unit tests and regression tests before core logic.
2. **Plan with user**: For large/complex changes, plan first — interact with the user to clarify requirements and eliminate ambiguity before generating code.
3. **Break down big tasks**: Split large/complex requirements into smaller tasks and complete incrementally.
4. **Git before edit**: Commit before modifying files for rollback.
5. **ASCII Art for UI**: Use ASCII art diagrams for UI mockups.
6. **DEBUG switch**: Ship in DEBUG mode first (Serilog file logging), key info logged to file. Switch to Release after tests pass.
7. **Version audit**: After major version commits, audit for missing required features.

## Version Roadmap

| Version | Focus | Key Deliverables |
|---------|-------|-----------------|
| V0.1 MVP | Core viewing & project persistence | File loading, hex view, text view, project save/open |
| V1.1 Core | Feature completion round 1 | Byte search, context menus, 18 built-in format rules, PE dynamic offsets, rule export, live preview, bookmarks, hex tools |
| V1.2 Core | Format rules + save/restore | Sequential mode for rules, 30+ format structure rules, structure tree save/restore, .ts dual detection, ISO 64KB header scan, hash calc deadlock fix |
| V1.3 Core | Array & repeating + more rules | Array loop mode, offsetFrom, lengthField, repeating structures, 35 format rules (RAR/PSD/PDF/OGG/Dump), file metadata panel, Windows Dump support |
| V1.5 Pro | Advanced analysis (deferred) | File diff (byte + field level), split views, theming, rule management, structure export (C struct/JSON), exception handling polish |
| **V1.6** | **Structure editing + format recognition** | **Structure tree management (edit/import/export), ZIP/RAR4/RAR5 container expansion, PDF/MP4/MKV/JPG format rules, async search, hex view performance optimization, encryption detection** |
| V1.7 | **Format coverage + multi-volume** | 55+ format rules, APK/EPUB/MOBI/AZW3/CRX/DMG/PYC/PAK/LNK/CAB/TAR/GZip/7z, EXE packer detection (20+), multi-volume ZIP lazy-loading, hex column header, 178 tests |
| V2.0 AI | Intelligent extension | LLM-powered structure recognition, batch classification |

## Development Conventions

- Commit messages reference requirements sections (e.g., `§2.3` for structure recognition)
- Follow the version roadmap strictly — don't implement V1.5+ features in V0.1 scope
- Start each version milestone with unit tests and regression tests per Development Rule #1
- When starting new code generation, ensure `dotnet build` passes with 0 errors first
