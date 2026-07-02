# Span Compression: Gap Analysis vs APM .NET Agent & Docs

Internal design/engineering note. Compares the `SpanCompressionProcessor` POC (branch `span-compression`)
against the documented Elastic APM span compression feature and the `apm-agent-dotnet` implementation it's
modeled on. Not published/user-facing documentation.

Sources:
- Docs: <https://www.elastic.co/docs/solutions/observability/apm/spans#apm-spans-span-compression>
- Agent: `C:\Code\Elastic\apm-agent-dotnet` (`src/Elastic.Apm/Model/Span.cs`, `Transaction.cs`, `Config/ConfigConsts.cs`)
- This repo: `src/Elastic.OpenTelemetry/Processors/SpanCompressionProcessor.cs`,
  `src/Elastic.OpenTelemetry/Extensions/ActivityExtensions.cs`, `Composite.cs`

## 1. What the docs require

A span is compression-eligible only if it:
1. **is an exit span**,
2. **has not propagated trace context**,
3. **outcome is not failure**.

Two strategies:

| Strategy | Match criteria | Default max duration | Enabled by default |
|---|---|---|---|
| `exact_match` | name + type + subtype + `destination.service.resource` | 50ms | Yes |
| `same_kind` | type + subtype + `destination.service.resource` (name may differ) | 0ms (disabled) | No |

## 2. How apm-agent-dotnet implements it

Key file: `src/Elastic.Apm/Model/Span.cs`.

- **`IsExitSpan`** (line 249) is a read-only flag set via a constructor parameter (line 69/85), passed in
  explicitly by *each instrumentation module* at span-creation time — SqlClient, MongoDb, Elasticsearch,
  GrpcClient, Azure ServiceBus, Azure CosmosDb, Kafka/RabbitMQ producers all call
  `StartSpanInternal(..., isExitSpan: true)`. It is never inferred after the fact — the instrumentation
  author declares it.
- **`_hasPropagatedContext`** (line 42) is set lazily the moment the `OutgoingDistributedTracingData`
  property getter is read (line 282) — i.e. the instant the instrumentation actually injects a `traceparent`
  into an outgoing call. This is what disqualifies HTTP/gRPC calls in practice.
- **Eligibility**: `IsCompressionEligible() => IsExitSpan && !_hasPropagatedContext && Outcome is Success or Unknown` (line 692).
- **`IsSameKind`** (lines 621-624): `Type == Type && Subtype == Subtype && Context.Service.Target == Context.Service.Target`
  — generic across every span category, because every APM span always has a populated Type/Subtype/Target.
- **`same_kind` composite naming**: renamed to `"Calls to " + destinationServiceResource` (line 650), e.g. `Calls to postgresql/mydb`.
- **Buffer**: a plain field (`_compressionBuffer` on Span, `CompressionBuffer` on Transaction) — the agent
  owns its own span model so no weak-table/identity tricks are needed. Flow in `End()` (lines 504-546):
  buffer empty → store this span as buffer; buffer occupied → try `TryToCompress`; on failure, flush the
  buffered span and store this one as the new buffer.
- **Related but separate feature — `ExitSpanMinDuration`**: discardable exit spans under a minimum duration
  are silently *dropped* rather than compressed, to cut noise from very fast successful exit calls.
- **Config** (`ConfigConsts.cs` lines 39-43): `SpanCompressionEnabled` (default `true`),
  `SpanCompressionExactMatchMaxDuration` (default `50ms`), `SpanCompressionSameKindMaxDuration` (default `0ms`),
  all standard env-var-configurable settings.

## 3. Gaps found in `SpanCompressionProcessor.cs`

### Critical

**"Exit span" is not actually checked.**
`IsCompressionEligible` (`SpanCompressionProcessor.cs:211-213`) only checks `Status` and `Duration` — it
never checks `ActivityKind`. A `Server`/`Consumer`/`Internal` leaf span (an entry span, or a manually created
internal span with no children) can be compressed today, directly violating doc requirement #1. The only
exclusion in the whole file is one narrow case: `Kind == Client && has "http.request.method"` (line 161).
There's no equivalent exclusion for gRPC (`rpc.system`) or other context-propagating client calls.

**The processor isn't wired into the pipeline.**
`TracerProviderBuilderExtensions.cs` only registers `TransactionIdProcessor` and `ElasticCompatibilityProcessor`.
`SpanCompressionProcessor` is never added, so the feature is inert today regardless of correctness. There is
also no `ElasticOpenTelemetryOptions` toggle or duration configuration — everything is a hardcoded `const`
(lines 40-44), unlike the agent's three env-configurable settings.

### Bug

**`TagsEqual` is inverted** (`ActivityExtensions.cs:93-111`):

```csharp
foreach (var tag in currentTags)
{
    if (otherTags.TryGetValue(tag.Key, out var otherTag))
        return false;   // <-- returns false when the tag IS found
    if (!tag.Equals(otherTag))
        return false;
}
```

This returns `false` as soon as a shared tag *is* found, and only proceeds to compare when it's *missing*
(comparing against `default`). In practice, `exact_match` compression will fail to trigger for almost any
two spans that share tags — the opposite of intended. Compounding this, `ToDictionary(t => t)` keys the
dictionary by the whole `KeyValuePair` rather than `t.Key`, so `TryGetValue(tag.Key, ...)` is looking up a
`KeyValuePair` key using a bare string — this compiles only because `KeyValuePair<string,object>` and
`string` both satisfy the generic constraint loosely at the call site, but the lookup semantics are not what
the code intends.

### Moderate

**`IsSameKind` only understands DB spans, and degrades unsafely** (`ActivityExtensions.cs:113-202`).
It extracts `db.system` / `db.collection.name` / `server.address` and falls back to `null == null → true`
when none are present. Two unrelated `Client`-kind spans with no db tags (e.g. a Redis `SET` and a generic
gRPC call) pass `IsSameKind` purely because all three comparisons trivially match on nulls — over-aggressive
`same_kind` grouping for non-DB exit spans. The agent avoids this because `Type`/`Subtype`/`Target` are
always populated for every span.

**Same-kind composite naming is a static placeholder.**
Hardcoded to `"Compressed calls"` (`ActivityExtensions.cs:115`) instead of a resource-derived name like the
agent's `"Calls to <resource>"` — even though the db tags needed to build that name are already extracted
right there.

### Minor

**Config default mismatch.** `SpanCompressionSameKindMaxDurationMs = 50`
(`SpanCompressionProcessor.cs:41`, already flagged by the author's own
`// TODO; Set to 0 by default, per existing agent`) vs. the documented/agent default of `0` (disabled).

### Architectural (not straightforwardly fixable)

Because this is a generic post-hoc OTel `Processor` rather than logic built into span creation, a
compressed/composite span must be re-emitted as a brand-new `Activity` with a **new SpanId**
(`FlushBuffer`, `SpanCompressionProcessor.cs:249`) — the original first span's identity is lost. The agent
never has this problem since the *same* `Span` object accumulates the composite in place. This is an
inherent cost of building compression as an SDK processor on top of `System.Diagnostics.Activity`, which
cannot be "un-stopped" and re-recorded once stopped.

## 4. The core challenge: inferring exit spans without instrumentation cooperation

apm-agent-dotnet's instrumentation modules *declare* `isExitSpan: true` when they create a span — ground
truth from the code that made the call. A generic `BaseProcessor<Activity>` only sees `OnEnd` after the
fact and has no such signal. Two complementary techniques close most of that gap.

### 4.1 Cheap first pass: `ActivityKind` + semantic-convention attributes

- Require `ActivityKind` is `Client` or `Producer` (never `Server`/`Consumer`/`Internal`) — closes the
  "any leaf span" hole described above.
- **Check order matters.** Several real exit spans carry both `http.*` and data-store attributes at once
  (Elasticsearch, CosmosDB, DynamoDB-over-HTTP, and similar cloud/search SDKs transport over HTTP but are
  semantically DB calls, and the agent explicitly flags Elasticsearch as `isExitSpan: true`). If
  `http.request.method` is treated as an absolute disqualifier *before* checking for `db.system` /
  data-store semconv attributes, these spans get wrongly excluded. Data-store attributes should take
  priority over a bare `http.request.method` presence.
- `rpc.system` (gRPC and other RPC frameworks) should be excluded the same way `http.request.method` is,
  since OTel's gRPC client instrumentation deterministically injects trace metadata on every call.

### 4.2 Precise signal: wrap the propagator, not the tags

.NET has two propagation choke points; outbound context injection always goes through one of them:

1. **`System.Diagnostics.DistributedContextPropagator.Current`** (BCL) — `HttpClient`'s `DiagnosticsHandler`
   calls `Current.Inject(activity, carrier, setter)` on every outbound request when a W3C-format `Activity`
   is active, independent of OpenTelemetry. The signature passes the `Activity` instance directly.
2. **`OpenTelemetry.Context.Propagation.Propagators.DefaultTextMapPropagator`** (OTel API) — used by OTel
   instrumentation libraries that don't rely on #1 (gRPC client, messaging instrumentations such as
   Confluent.Kafka/MassTransit, custom app code). `Inject(PropagationContext, carrier, setter)` doesn't hand
   you the `Activity` directly, but it's invoked while that span is still `Activity.Current`.

Both are swappable static singletons. A distro already owns composition at `.Build()` time and could wrap
whichever propagator is configured with a decorator that does
`activity?.SetCustomProperty("HasPropagatedContext", true)` inside `Inject`, then delegates to the real
propagator. `SpanCompressionProcessor.OnEnd` then just checks that property — directly analogous to the
agent's `_hasPropagatedContext` flip inside `OutgoingDistributedTracingData`. Timing is safe: injection
always happens *during* the span's lifetime, before `Stop()`/`OnEnd` runs.

Confirmed via search: this repo does not currently touch either propagator API (`src/` has zero references
to `DistributedContextPropagator` or `TextMapPropagator`), so this would be new territory, but it fits the
role a distro already plays.

**Coverage this actually gets you, more than initially assumed:**
On modern .NET (Core/5+), `HttpWebRequest` and `WebClient` are thin wrappers over `SocketsHttpHandler`
internally, so legacy call sites still route through `DiagnosticsHandler` and still hit
`DistributedContextPropagator.Current`. The propagator hook catches them for free. This is only a real gap
on classic .NET Framework.

**Real, stated limitations:**
- Only catches propagation done through these two blessed APIs. Anything injecting trace headers another
  way (hand-rolled header injection, embedding a trace id in a DB comment or custom message property
  manually) slips through undetected and would look wrongly "safe" to compress.
- It's a global, process-wide mutation — needs to be wrapped late enough in `.Build()` to not fight with
  anything the host app configures itself.
- Strictly more accurate than the tag-based guess in §4.1, but still a generic signal bolted onto a system
  that wasn't built to expose this; it can't be made airtight the way agent-declared `isExitSpan` is.

### 4.3 The remaining tail: a documented manual override

For hand-rolled/manual context propagation that bypasses both propagator APIs, there is no generic
detection mechanism short of scanning outbound bytes for trace-id-shaped strings — impractical, fragile,
and privacy-questionable. The pragmatic answer is the same pattern already half-present in this file: the
existing `GetCustomProperty("SkipCompression")` escape hatch (`SpanCompressionProcessor.cs:79-80`, documented
as "a mechanism for consumers to mark spans as ineligible") can be extended with a symmetric property (e.g.
`SetCustomProperty("HasPropagatedContext", true)`) that custom/third-party instrumentation authors can set
directly when they know they've handed out a span's identity through a channel we can't observe.

### 4.4 Recommended layering

1. `Kind`/semconv check (cheap, catches the obvious HTTP/gRPC cases before doing any propagator lookup;
   correctly prioritizes data-store attributes over a bare `http.request.method`).
2. Propagator-wrapper flag (catches everything routed through `DistributedContextPropagator` or
   `Propagators.DefaultTextMapPropagator` — the large majority of real instrumentation, including legacy
   `WebRequest` on modern runtimes).
3. Documented manual-override property for the long tail of hand-rolled propagation.

## 5. Is scoping to DB spans alone (`db.system` present) sufficiently useful as a first step?

Yes, as a v1. Scoping eligibility to "any span carrying `db.system`" (covers SQL, Mongo, Elasticsearch,
Redis, CosmosDB, Cassandra, etc. — not just relational):

- Targets the single highest-value real-world case: N+1 query storms, the textbook motivating example in
  the docs themselves.
- Needs none of the context-propagation machinery in §4 — DB wire protocols don't carry W3C trace headers,
  so this subset is safe by construction.
- Is a simple positive allowlist rather than a denylist trying to enumerate every propagating category
  correctly.

What it gives up vs. full agent parity:
- **Messaging producers** (Kafka, RabbitMQ, Azure ServiceBus publish spans) — `Producer` kind,
  `messaging.system` present, no `db.*`. Genuinely lost under DB-only scoping.
- **HTTP-transport data stores** (Elasticsearch, CosmosDB) — *not* lost as long as data-store attributes are
  checked with priority over `http.request.method` (see §4.1); lost only under a naive ordering.

## 6. Recommended phasing

1. **Fix correctness bugs now, independent of scope decisions**: `TagsEqual` inversion, `same_kind` default
   duration (50ms → 0ms), wire `SpanCompressionProcessor` into the pipeline behind a config toggle.
2. **Ship `db.system`-scoped compression** as a correct replacement for today's "compresses any leaf span"
   behavior — low risk, high value, no propagator work required.
3. **Broaden with the propagator-wrapper mechanism** (§4.2) once the above is stable, to safely include
   messaging producers and other non-DB exit-like spans without reintroducing the context-propagation risk
   that motivated the original DB-only scoping.
