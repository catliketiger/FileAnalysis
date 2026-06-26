# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Windows desktop tool for binary file structure analysis — automatically identifies and visualizes the internal layout of unknown binary files through signature matching and heuristic inference, with a hex view + structure tree as the core interaction model.

**Current status**: V1.2. C# .NET 10 WPF, with dual-engine structure recognition, 30+ predefined format structure rules with sequential/schema support, structure tree save/restore, PE dynamic offset handling, 87+ xUnit tests passing.

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
│   ├── FileStruct.Core.Tests/    # BinaryBuffer tests
│   ├── FileStruct.Services.Tests/ # Service unit tests
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

1. **TDD-first**: Write unit/integration tests before core logic.
2. **Plan with user**: For large/complex changes, plan first — interact with the user to clarify requirements and eliminate ambiguity before generating code.
3. **Git before edit**: Commit before modifying files for rollback.
4. **ASCII Art for UI**: Use ASCII art diagrams for UI mockups.
5. **DEBUG switch**: Ship in DEBUG mode first (Serilog file logging), switch to Release after tests pass.
6. **Version audit**: After major version commits, trigger a version audit to verify no required features were missed.

## Version Roadmap

| Version | Focus | Key Deliverables |
|---------|-------|-----------------|
| V0.1 MVP | Core viewing & project persistence | File loading, hex view, text view, project save/open |
| V1.1 Core | Feature completion round 1 | Byte search, context menus, 18 built-in format rules, PE dynamic offsets, rule export, live preview, bookmarks, hex tools |
| V1.2 Core | Format rules + save/restore | Sequential mode for rules, 30+ format structure rules (SQLite/ISO/OLE2/ICO/Mach-O/WOFF/DEB/AIFF/RTF/MPEG-TS), structure tree save/restore in .fstruct, .ts dual detection (TS/MPEG-TS), ISO 64KB header scan, hash calc deadlock fix, DTO-based serialization |
| V2.0 AI | Intelligent extension | LLM-powered structure recognition, batch classification |

## Development Conventions

- Commit messages reference requirements sections (e.g., `§2.3` for structure recognition)
- Follow the version roadmap strictly — don't implement V1.5+ features in V0.1 scope
- Start each version milestone with test cases per Development Rule #1
- When starting new code generation, ensure `dotnet build` passes with 0 errors first
