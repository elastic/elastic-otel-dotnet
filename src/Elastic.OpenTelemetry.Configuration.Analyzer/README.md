# Elastic OpenTelemetry Configuration Analyzer

A set of Roslyn analyzers for enforcing best practices when configuring Elastic and OpenTelemetry in .NET applications using EDOT .NET.

This package provides the following analyzers:

---
## 1 ElasticChainingAnalyzer (`EDOT001`)

Warns if a method starting with `WithElastic` is called after or inside a method named `AddElasticOpenTelemetry` or `AddOpenTelemetry`.

**Example:**
```csharp
builder.Services.AddElasticOpenTelemetry()
    .WithElasticTracing(...); // Warning

builder.Services.AddOpenTelemetry()
    .WithElasticTracing(...); // Warning
```

### Best Practice

Do not call `WithElastic*` methods after or inside `AddElasticOpenTelemetry` or `AddOpenTelemetry`.

---

## 2. NestedWithElasticAnalyzer (EDOT002)

Warns if a method starting with WithElastic is called inside another method starting with WithElastic.

**Example:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithElasticLogging(t =>
        t.WithElasticDefaults(builder.Configuration)); // Warning on WithElasticDefaults
```

### Best Practice

Avoid nesting WithElastic* calls inside other WithElastic* calls.

---

## 3. MultipleOpenTelemetryInMethodAnalyzer (EDOT003)

Warns if AddOpenTelemetry or AddElasticOpenTelemetry is called more than once in the same method or in top-level statements.

**Example:**
```csharp
builder.Services.AddOpenTelemetry(); // Warning
builder.Services.AddElasticOpenTelemetry(); // Warning
builder.Services.AddOpenTelemetry(); // Warning
```

### Best Practice

Call each configuration method only once per method or top-level block.
