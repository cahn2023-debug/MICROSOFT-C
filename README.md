# Offline Project Manager (C# Edition)

This is the C# port of the Offline Project Manager application, built with .NET Framework 4.8 and WPF.

## Prerequisites

- Windows 10/11
- Visual Studio 2022 (or 2019)
- .NET Framework 4.8 Runtime/Dev Pack

## Project Structure

- **Models**: Entity Framework 6 database entities (`Project`, `FileEntry`, `Task`...).
- **Data**: `AppDbContext` and SQLite configuration.
- **Services**: Core business logic (`ProjectService`, `SearchService`...).
- **ViewModels**: MVVM logic (`MainViewModel`, `ProjectExplorerViewModel`...).
- **Views**: WPF XAML UI components.
- **Utils**: Helpers (Vietnamese text processing, encoding).

## Getting Started

1. Open `OfflineProjectManager.sln` in Visual Studio.
2. Restore NuGet Packages. The project relies on:
   - `EntityFramework` (6.4.4)
   - `System.Data.SQLite`
   - `CommunityToolkit.Mvvm` (8.x)
   - `AvalonEdit`
   - `PdfiumViewer`
3. Build the Solution (Ctrl+Shift+B).
4. Run the application (F5).

## Key Features Implemented

- **Database**: Full SQLite schema compatibility with Python version (.pmp files).
- **Search**: Hybrid search (Filename + Content) with Vietnamese accent support (e.g., "đá" matches "da").
- **Task Management**: Create tasks and notes linked to files.
- **Preview**: Basic architecture for file previewing.

## Notes for Developers

- The application uses Manual Dependency Injection in `App.xaml.cs`.
- `ProjectService` handles the creation and schema initialization of `.pmp` files using SQL DDL commands to ensure compatibility.
- Ensure `x64` or `x86` build target matches the installed SQLite interop DLLs if runtime errors occur.
