# Design Required

A list of problems which require further API design.

## Item 1

What if a user wants to use the simplified AgentBuilder e.g:

```
using var agent = new AgentBuilder(ActivitySourceName).Build();
```

but then wants to add one extra instrumentation (e.g. Redis)? This is achieved with an extension method
`AddRedisInstrumentation` on the TracerProviderBuilder.

Right now, the consumer would have to use the more complex overload:

```
public AgentBuilder ConfigureTracer(Action<TracerProviderBuilder> configure)
```

This especially applies if we don't auto listen to ASP.NET Core which means that for very common scenarios,
users are forced to use the more complex overload. Is there anything we can do to make that easier?
