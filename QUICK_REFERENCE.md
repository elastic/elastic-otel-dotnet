# Quick Reference: OpAmp Abstractions Implementation

## TL;DR

Created a new abstraction layer (`Elastic.OpenTelemetry.OpAmp.Abstractions`) that solves ALC boundary issues by using **primitives only** for inter-ALC communication.

**Problem**: Couldn't pass OpAmp types across AssemblyLoadContext boundaries  
**Solution**: Serialize to JSON bytes, pass only primitives

## File Map

| File | Purpose |
|------|---------|
| `src/Elastic.OpenTelemetry.OpAmp.Abstractions/` | NEW abstraction assembly |
| `IOpAmpMessageSubscriber.cs` | Public interface (primitives only) |
| `OpAmpMessageSubscriberFactory.cs` | Factory for creating instances |
| `OpAmpMessageSubscriberImpl.cs` | Handles OpAmp types inside isolated ALC |
| `ARCHITECTURE.md` | Detailed design explanation |

## Modified Files

| File | Change |
|------|--------|
| `CentralConfiguration.cs` | Loads abstractions assembly, subscribes to events |
| `RemoteConfigMessageListener.cs` | Processes JSON payloads from isolated ALC |
| `build/scripts/Packaging.fs` | Copies new DLLs to redistributable |

## Build & Deploy

```bash
# Development build
dotnet build

# Redistributable build (includes new DLLs for NET8.0)
build.bat redistributable

# Verify DLLs included
dir .artifacts\elastic-distribution\v*\net\Elastic.OpenTelemetry.OpAmp*.dll
```

## How It Works

```
1. CentralConfiguration loads abstractions assembly from isolated ALC
2. Calls OpAmpMessageSubscriberFactory.Create() via reflection
3. Subscribes to IOpAmpMessageSubscriber events using dynamic
4. Receives RemoteConfigMessage as JSON bytes
5. RemoteConfigMessageListener processes JSON payload
```

## Key Design Decisions

✅ **Primitives cross ALC boundaries** - No type validation issues  
✅ **JSON serialization** - Human-readable, language-agnostic  
✅ **Factory pattern** - Clean instantiation across ALC boundary  
✅ **Event-based** - Decoupled communication  
✅ **NET8.0 only** - Uses System.Text.Json and ALC features  

## Testing Checklist

- [ ] `dotnet build` succeeds
- [ ] Abstractions DLL builds: `.artifacts/bin/Elastic.OpenTelemetry.OpAmp.Abstractions/`
- [ ] `build.bat redistributable` completes
- [ ] New DLLs in final redistributable
- [ ] Deploy to AutoInstrumentation directory
- [ ] Run profiler-loaded app
- [ ] Check logs for "OpAmp client initialization started"
- [ ] Monitor for message reception

## Documentation

| Document | Content |
|----------|---------|
| `ARCHITECTURE.md` | Message flow, design rationale |
| `BUILD_OPAMP_ABSTRACTIONS.md` | Build process, packaging details |
| `OPAMP_ABSTRACTIONS_SUMMARY.md` | Complete implementation overview |
| `VERIFICATION_CHECKLIST.md` | Testing verification items |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Build fails | Run `dotnet clean && dotnet build` |
| Missing DLLs | Ensure `dotnet build -c Release` was run |
| DLLs not in redistributable | Check `build/scripts/Packaging.fs` injections |
| Runtime errors | Check logs for assembly loading failures |

## Contact Points

**Initialization**: `CentralConfiguration.cs` line ~125  
**Message Handling**: `RemoteConfigMessageListener.cs` line ~25  
**Factory**: `OpAmpMessageSubscriberFactory.Create()`  
**Build**: `build/scripts/Packaging.fs` functions  

## Status

✅ **Complete and Ready for Testing**

Build successful, documentation complete, ready for proof of concept deployment.
