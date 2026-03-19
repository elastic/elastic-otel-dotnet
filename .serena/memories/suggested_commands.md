# Suggested Commands

## Build
```bash
# Build the solution (release)
dotnet build -c release

# Build via FAKE script (Windows)
./build.bat build

# Build via FAKE script (bash/WSL)
./build.sh build
```

## Test
```bash
# Run all tests
dotnet test -c release

# Run unit tests only
dotnet test -c release --filter "FullyQualifiedName~.Tests"

# Run integration tests only
dotnet test -c release --filter "FullyQualifiedName~.IntegrationTests"

# Via build script
./build.bat test
```

## Format & Lint
```bash
# Format code
dotnet format

# Check formatting (CI-style, no changes)
dotnet format --verify-no-changes
```

## Package
```bash
# Create NuGet packages
dotnet pack
```

## Clean
```bash
dotnet clean -c release
```

## Version Info
```bash
./build.bat version
```

## Utility Commands (Windows with bash shell)
- `git` — version control
- `ls` / `find` / `grep` — file operations (bash on Windows)
- Artifacts output in `.artifacts/` directory
