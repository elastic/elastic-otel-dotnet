# Code Style & Conventions

## Indentation
- **C# files**: Tabs (indent_size=4)
- **F# files**: Spaces (indent_size=4)
- **JSON, csproj, props, targets, markdown, yml**: Spaces (indent_size=2)

## C# Naming Conventions
- **Constants**: PascalCase
- **Non-public static fields**: PascalCase
- **Non-private readonly fields**: PascalCase
- **Private instance fields**: _camelCase (underscore prefix)
- **Locals and parameters**: camelCase
- **Local functions**: PascalCase
- **Public members**: PascalCase

## C# Style Rules (enforced as errors)
- Always use `var` (for built-in types, apparent types, and elsewhere)
- Prefer expression-bodied members (methods, constructors, operators, properties, indexers, accessors)
- Use modern language features: pattern matching, inlined variable declaration, deconstructed variables, throw expressions, conditional delegate calls
- No `this.` qualification (error severity)
- Use language keywords instead of framework type names (e.g., `string` not `String`)
- Use object/collection initializers, tuple names, null propagation, coalesce expressions
- Braces optional for single-line statements (warning)
- Allman brace style (opening brace on new line)

## Other
- `TreatWarningsAsErrors=true`
- Trim/AOT analyzer warnings IL3050 and IL2026 set to error severity
- Sort `System.*` using directives first
- License header expected: "Licensed to Elasticsearch B.V under one or more agreements."
