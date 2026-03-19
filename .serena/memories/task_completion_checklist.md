# Task Completion Checklist

When completing a coding task in this project, ensure the following:

1. **Build succeeds**: `dotnet build -c release` — warnings are treated as errors
2. **Formatting**: Run `dotnet format` to fix any formatting issues, or verify with `dotnet format --verify-no-changes`
3. **Tests pass**: `dotnet test -c release` (or more targeted filter if only certain tests are relevant)
4. **Trim/AOT compatibility**: IL3050 and IL2026 are errors — ensure no trim/AOT warnings are introduced
5. **Code style**: Follow the `.editorconfig` conventions (var everywhere, expression-bodied members, Allman braces, _camelCase for private fields)
6. **No new warnings**: `TreatWarningsAsErrors` is enabled globally
