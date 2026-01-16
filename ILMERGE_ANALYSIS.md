# ILMerge Dependency Analysis

## Where OpAmpClient is Referenced in AutoInstrumentation

### Call Chain:
1. **AutoInstrumentationPlugin.cs** (static constructor)
   ```csharp
   static AutoInstrumentationPlugin()
   {
       Components = ElasticOpenTelemetry.Bootstrap(...);  // Line 38
   }
   ```

2. **ElasticOpenTelemetry.cs** (Bootstrap method)
   - Calls `CreateComponents()`
   - Which creates `CentralConfiguration` if OpAmp is enabled

3. **CentralConfiguration.cs** (Direct OpAmpClient usage!)
   ```csharp
   using OpenTelemetry.OpAmp.Client;                    // LINE 9
   using OpenTelemetry.OpAmp.Client.Messages;           // LINE 10
   using OpenTelemetry.OpAmp.Client.Settings;           // LINE 11
   
   internal sealed class CentralConfiguration : IDisposable, IAsyncDisposable
   {
       private readonly OpAmpClient _client;             // LINE 26 - DIRECT TYPE REFERENCE
       
       public CentralConfiguration(CompositeElasticOpenTelemetryOptions options, CompositeLogger logger)
       {
           // Uses OpAmpClient directly
           _client = new OpAmpClient(opts => {...});     // LINE 46
       }
   }
   ```

## The Problem

**AutoInstrumentation DOES directly reference OpAmpClient types:**

- `CentralConfiguration.cs` is **shared code** that's compiled into both:
  - `Elastic.OpenTelemetry.dll` 
  - `Elastic.OpenTelemetry.AutoInstrumentation.dll` (via `<Compile Include="..\Elastic.OpenTelemetry.Core\**\*.cs" />`)

- This shared code has **direct using statements** and **direct type references** to `OpenTelemetry.OpAmp.Client`

- When AutoInstrumentation's static constructor runs, it calls `Bootstrap()` → `CreateComponents()` → creates `CentralConfiguration` instance
- The CLR immediately needs to load `OpenTelemetry.OpAmp.Client` assembly to resolve the `OpAmpClient` type

## Why ILMerge Can't Merge OpAmpClient into AutoInstrumentation

When ILMerge tries to merge `OpenTelemetry.OpAmp.Client.dll` into `Elastic.OpenTelemetry.AutoInstrumentation.dll` with `/internalize`:

1. It successfully internalizes the OpAmp types
2. But `Elastic.OpenTelemetry.AutoInstrumentation.dll` still has **metadata that references the external assembly** `OpenTelemetry.OpAmp.Client`
3. Even though the types are now internal, the IL still expects an external assembly reference
4. This causes the error: *"The assembly 'OpenTelemetry.OpAmp.Client' was not merged in correctly. It is still listed as an external reference in the target assembly."*

## Solution Architecture

### Option 1: Keep OpAmpClient External (Current)
✅ **AutoInstrumentation:**
- Merge: Google.Protobuf ✓
- External: OpenTelemetry.OpAmp.Client (unresolved at runtime)

⚠️ **Problem:** Runtime `FileNotFoundException` for OpAmpClient

### Option 2: Include Both Merged Versions in Redistributable
✅ **Redistributable zip should contain:**
1. `Elastic.OpenTelemetry.dll` (has merged OpAmp.Client + Protobuf)
2. `Elastic.OpenTelemetry.AutoInstrumentation.dll` (has merged Protobuf only)
3. `OpenTelemetry.OpAmp.Client.dll` (external reference)

The AutoInstrumentation code can load OpAmpClient from this external DLL at runtime.

### Option 3: Refactor to Avoid Direct OpAmpClient Usage in AutoInstrumentation
- Move OpAmp-dependent code to separate assembly or interface
- Have AutoInstrumentation reference an abstraction instead of OpAmpClient directly
- This would allow merging but requires significant refactoring

## Recommendation

**Use Option 2** - The current setup is actually correct:

- `Elastic.OpenTelemetry` is the **main package** that has merged OpAmp.Client
- `Elastic.OpenTelemetry.AutoInstrumentation` is the **auto-instrumentation plugin** that shares core code
- Both should be included in the **same redistributable** because:
  1. They're designed to work together
  2. AutoInstrumentation's initialization code needs OpAmpClient
  3. The merged version in the main package is what provides the types at runtime

The redistributable should be structured as:
```
elastic-otel-dotnet/
├── Elastic.OpenTelemetry.dll              (merged: Protobuf, OpAmp.Client)
├── Elastic.OpenTelemetry.AutoInstrumentation.dll  (merged: Protobuf)
├── OpenTelemetry.OpAmp.Client.dll         (external reference for auto-instrumentation)
└── [other dependencies]
```
