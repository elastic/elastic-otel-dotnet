# OpAmp Abstractions Architecture

## Overview

This document describes the new OpAmp abstraction layer that solves AssemblyLoadContext (ALC) boundary crossing issues by using only primitive types for inter-ALC communication.

## Problem Solved

When using an isolated AssemblyLoadContext to load `OpenTelemetry.OpAmp.Client` and its dependencies, calling methods on objects from that ALC with interface parameters from the default ALC fails because:

1. **Interface type identity**: `IOpAmpListener<RemoteConfigMessage>` from the isolated ALC is a different type than the same interface from the default ALC
2. **Reflection type checking**: `MethodInfo.Invoke()` performs strict type validation on parameters
3. **Dynamic limitations**: The C# runtime binder cannot infer generic type parameters from dynamic arguments

## Solution: Primitive-Only Interfaces

Instead of trying to pass complex types across ALC boundaries, we created `Elastic.OpenTelemetry.OpAmp.Abstractions` which:

- **Lives in the isolated ALC** and owns all OpAmp types
- **Exposes a primitive-only interface**: `IOpAmpMessageSubscriber`
- **Uses only primitives across boundaries**: `string`, `byte[]`, `Action<T>` delegates
- **Marshals messages internally**: JSON serialization of `RemoteConfigMessage` objects

## Architecture

### Components

1. **Elastic.OpenTelemetry.OpAmp.Abstractions.dll** (Isolated ALC)
   - `IOpAmpMessageSubscriber` interface (public)
   - `OpAmpMessageSubscriberFactory.Create()` method (public)
   - `OpAmpMessageSubscriberImpl` implementation (internal)
   - Fully handles OpAmp client lifecycle and message marshalling

2. **Elastic.OpenTelemetry.Core.Configuration.CentralConfiguration.cs** (Default ALC)
   - Loads the abstractions assembly in the isolated ALC via reflection
   - Calls factory method to create subscriber instance
   - Subscribes to events using only primitive parameters
   - Delegates message handling to `RemoteConfigMessageListener`

3. **Elastic.OpenTelemetry.Core.Configuration.RemoteConfigMessageListener.cs** (Default ALC)
   - Extended with `HandleMessage(string messageType, byte[] jsonPayload)` method
   - Receives and processes JSON-serialized messages
   - Bridges the gap between isolated ALC and default ALC

## Message Flow

```
┌─────────────────────────────────────────────┐
│  Isolated AssemblyLoadContext               │
│  ┌──────────────────────────────────────┐   │
│  │ OpAmpClient (from isolated ALC)       │   │
│  │   ↓                                   │   │
│  │ OpAmpMessageSubscriberImpl             │   │
│  │   (implements IOpAmpListener<T>)      │   │
│  │   ↓                                   │   │
│  │ Serialize to JSON with System.Text.Json │ │
│  │   ↓ (primitives cross boundary)       │   │
│  └───┬────────────────────────────────────┘   │
│      │ Action<string, byte[]>                 │
└──────┼──────────────────────────────────────────┘
       │
       ↓
┌──────────────────────────────────────────────┐
│  Default AssemblyLoadContext                 │
│  ┌──────────────────────────────────────┐    │
│  │ CentralConfiguration                  │    │
│  │   ↓                                   │    │
│  │ RemoteConfigMessageListener           │    │
│  │   .HandleMessage(type, jsonPayload)  │    │
│  └──────────────────────────────────────┘    │
└──────────────────────────────────────────────┘
```

## Integration Points

### 1. CentralConfiguration Initialization

```csharp
// Load abstractions assembly from isolated ALC
var abstractionsAssembly = loadContext.LoadFromAssemblyPath(abstractionsAssemblyPath);

// Use reflection to call factory method (safe with primitives)
var factoryType = abstractionsAssembly.GetType("...OpAmpMessageSubscriberFactory");
var createMethod = factoryType.GetMethod("Create", ...);
var subscriber = createMethod.Invoke(null, new object[] { logger });

// Subscribe using dynamic (primitives = safe)
dynamic sub = subscriber;
sub.MessageReceived += (string type, byte[] payload) => { ... };
await sub.StartAsync(endpoint, token);
```

### 2. Message Reception

```csharp
// Event handler in default ALC receives only primitives
Action<string, byte[]> onMessageReceived = (messageType, payload) =>
{
    _remoteConfigListener.HandleMessage(messageType, payload);
};
```

### 3. Message Processing

```csharp
internal void HandleMessage(string messageType, byte[] jsonPayload)
{
    // Deserialize JSON payload (now in default ALC context)
    var json = Encoding.UTF8.GetString(jsonPayload);
    // Process remote configuration
}
```

## Why This Works

**Primitives are safe across ALC boundaries because:**
- ✅ `string` - No type identity issues
- ✅ `byte[]` - Simple value type array
- ✅ `Action<primitive>` - Delegates work with any compatible method signature
- ✅ No interface type checking - Runtime can't tell the difference

**Complex types fail because:**
- ❌ Interface types have ALC-specific identities
- ❌ `RemoteConfigMessage` from isolated ALC ≠ `RemoteConfigMessage` from default ALC
- ❌ Reflection validates interface implementation at invoke time

## Deployment

The abstractions assembly must be deployed to the AutoInstrumentation redistributable directory:

```
${OTEL_DOTNET_AUTO_INSTALL_DIR}/net/
├── Elastic.OpenTelemetry.OpAmp.Abstractions.dll
├── Elastic.OpenTelemetry.OpAmp.Abstractions.pdb
├── Elastic.OpenTelemetry.OpAmp.Abstractions.deps.json
├── Elastic.OpenTelemetry.AutoInstrumentation.dll
├── OpenTelemetry.OpAmp.Client.dll
├── Google.Protobuf.dll
└── [other dependencies]
```

The `deps.json` file helps the ALC resolve dependencies during assembly loading.

## Future Enhancements

1. **Automatic message deserialization**: Instead of just logging JSON, fully reconstruct `RemoteConfigMessage`
2. **Configuration application**: Apply received configuration changes to the running application
3. **Error recovery**: Implement reconnection logic for failed OpAmp connections
4. **Performance optimization**: Consider message batching or streaming for large payloads

## References

- [AssemblyLoadContext Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)
- [OpenTelemetry OpAmp Protocol](https://github.com/open-telemetry/opampproto)
