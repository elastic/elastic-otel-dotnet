// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Reflection;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;

namespace Elastic.OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// Validates that bootstrap flows which resolve to existing components do not leave behind an orphaned
/// pre-activation <see cref="CompositeLogger"/> whose safety timer later creates a second file and console logger.
/// Grouped into the singleton collection as these tests exercise the static <c>PreActivationInstance</c>.
/// </summary>
[Collection("CompositeLoggerSingleton")]
public class CompositeLoggerOrphanTests
{
	private static CompositeElasticOpenTelemetryOptions CreateOpAmpOptions(ILogger additionalLogger) =>
		new(new Dictionary<string, string>
		{
			["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
			["ELASTIC_OTEL_OPAMP_ENDPOINT"] = "http://localhost:4320",
			["OTEL_SERVICE_NAME"] = "test-service",
		})
		{
			AdditionalLogger = additionalLogger
		};

	private static CompositeElasticOpenTelemetryOptions CreateNonOpAmpOptions(ILogger additionalLogger) =>
		new(new Dictionary<string, string>
		{
			["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
		})
		{
			AdditionalLogger = additionalLogger
		};

	[Fact]
	public void RetirePreActivationInstance_ForwardsDeferredEntries_AndPreventsSafetyTimerActivation()
	{
		var orphanSink = new CountingLogger();
		var adoptedSink = new CountingLogger();
		var options = CreateOpAmpOptions(orphanSink);

		using var adopted = new CompositeLogger(CreateNonOpAmpOptions(adoptedSink));
		CompositeLogger? orphan = null;

		try
		{
			using (ClaimSingletonSlot())
			{
				orphan = CompositeLogger.GetOrCreate(options);

				for (var i = 0; i < 3; i++)
					orphan.Log(LogLevel.Information, new EventId(i), $"pre-bootstrap {i}", null, (s, _) => s);

				var adoptedCountBefore = adoptedSink.Count;

				CompositeLogger.RetirePreActivationInstance(adopted, options);

				// 1 init debug (queued in ctor) + 3 queued entries forwarded to the adopted logger
				Assert.Equal(adoptedCountBefore + 4, adoptedSink.Count);
			}

			// The safety timer firing after retirement must not activate the orphan
			orphan.OnSafetyTimerElapsed();
			orphan.Log(LogLevel.Information, new EventId(99), "post-retire", null, (s, _) => s);

			Assert.Equal(0, orphanSink.Count);
			Assert.False(orphan.LogFileEnabled);
			Assert.False(orphan.IsEnabled(LogLevel.Information));

			// The retired orphan is no longer handed out as the singleton
			var next = CompositeLogger.GetOrCreate(options);
			Assert.NotSame(orphan, next);
			next.Dispose();
		}
		finally
		{
			orphan?.Dispose();
			CompositeLogger.ClearPreActivationInstance();
		}
	}

	[Fact]
	public void RetirePreActivationInstance_SafetyTimerClaimedActivation_RetiresAndPreventsSubLoggerCreation()
	{
		var orphanSink = new CountingLogger();
		var adoptedSink = new CountingLogger();
		var options = CreateOpAmpOptions(orphanSink);

		using var adopted = new CompositeLogger(CreateNonOpAmpOptions(adoptedSink));
		CompositeLogger? orphan = null;

		try
		{
			Thread timerCallback;
			int adoptedCountBefore;

			using (ClaimSingletonSlot())
			{
				orphan = CompositeLogger.GetOrCreate(options);

				for (var i = 0; i < 3; i++)
					orphan.Log(LogLevel.Information, new EventId(i), $"pre-bootstrap {i}", null, (s, _) => s);

				adoptedCountBefore = adoptedSink.Count;
				timerCallback = new Thread(orphan.OnSafetyTimerElapsed);

				// Holding the activation lock parks the timer callback after it has claimed activation but before it
				// creates sub-loggers. Lock is reentrant, so retirement can still proceed on this thread.
				using (GetField<Lock>(orphan, "_activationLock").EnterScope())
				{
					timerCallback.Start();
					var claimed = orphan;
					Assert.True(SpinWait.SpinUntil(() => GetField<int>(claimed, "_activatedViaActivateMethod") == 1, TimeSpan.FromSeconds(10)));

					CompositeLogger.RetirePreActivationInstance(adopted, options);
				}
			}

			Assert.True(timerCallback.Join(TimeSpan.FromSeconds(10)));

			// 1 init debug (queued in ctor) + 3 queued entries forwarded to the adopted logger
			Assert.Equal(adoptedCountBefore + 4, adoptedSink.Count);
			Assert.Equal(0, orphanSink.Count);
			Assert.Null(GetField<FileLogger?>(orphan, "_fileLogger"));
			Assert.False(orphan.HasAdditionalLogger);
		}
		finally
		{
			orphan?.Dispose();
			CompositeLogger.ClearPreActivationInstance();
		}
	}

	[Fact]
	public void RetirePreActivationInstance_SafetyTimerCreatedSubLoggers_DisposesThem()
	{
		var orphanSink = new CountingLogger();
		var options = CreateOpAmpOptions(orphanSink);

		using var adopted = new CompositeLogger(CreateNonOpAmpOptions(new CountingLogger()));
		CompositeLogger? orphan = null;

		try
		{
			Thread timerCallback;

			// Holding the singleton lock also parks the timer callback after it has created sub-loggers but before
			// it clears the singleton slot.
			using (ClaimSingletonSlot())
			{
				orphan = CompositeLogger.GetOrCreate(options);
				timerCallback = new Thread(orphan.OnSafetyTimerElapsed);

				timerCallback.Start();
				var activating = orphan;
				Assert.True(SpinWait.SpinUntil(() => GetField<object?>(activating, "_deferredQueue") is null, TimeSpan.FromSeconds(10)));
				Assert.True(orphan.HasAdditionalLogger);

				CompositeLogger.RetirePreActivationInstance(adopted, options);
			}

			Assert.True(timerCallback.Join(TimeSpan.FromSeconds(10)));

			var orphanCountAfterRetire = orphanSink.Count;
			orphan.Log(LogLevel.Information, new EventId(99), "post-retire", null, (s, _) => s);

			Assert.False(orphan.HasAdditionalLogger);
			Assert.Equal(orphanCountAfterRetire, orphanSink.Count);
		}
		finally
		{
			orphan?.Dispose();
			CompositeLogger.ClearPreActivationInstance();
		}
	}

	/// <summary>
	/// Clears the singleton slot and holds its lock (reentrant, so the calling thread can still use the slot) until
	/// disposed, so that bootstraps in concurrently running test classes cannot clear or replace the instance
	/// a test is about to retire.
	/// </summary>
	private static Lock.Scope ClaimSingletonSlot()
	{
		var scope = GetStaticField<Lock>("StaticLock").EnterScope();
		CompositeLogger.ClearPreActivationInstance();
		return scope;
	}

	private static T GetField<T>(CompositeLogger logger, string name) =>
		(T)typeof(CompositeLogger).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logger)!;

	private static T GetStaticField<T>(string name) =>
		(T)typeof(CompositeLogger).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

	[Fact]
	public void RetirePreActivationInstance_IncompatibleInstance_IsClearedButNotRetired()
	{
		CompositeLogger.ClearPreActivationInstance();

		var otherFlowSink = new CountingLogger();
		var otherFlowOptions = CreateOpAmpOptions(otherFlowSink);
		var otherFlowLogger = CompositeLogger.GetOrCreate(otherFlowOptions);

		using var adopted = new CompositeLogger(CreateNonOpAmpOptions(new CountingLogger()));

		try
		{
			// Options that are not equal to those of the pending instance — a different bootstrap flow.
			var bootstrapOptions = new CompositeElasticOpenTelemetryOptions(new Dictionary<string, string>
			{
				["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:9999",
			});

			CompositeLogger.RetirePreActivationInstance(adopted, bootstrapOptions);

			// The other flow can still activate its logger as before.
			otherFlowLogger.Activate(otherFlowOptions);
			Assert.True(otherFlowLogger.IsEnabled(LogLevel.Information));
			Assert.True(otherFlowSink.Count > 0);
		}
		finally
		{
			otherFlowLogger.Dispose();
			CompositeLogger.ClearPreActivationInstance();
		}
	}

	[Fact]
	public void GetLogger_ReusesServiceCollectionComponentsLogger_InsteadOfCreatingPreActivationLogger()
	{
		CompositeLogger.ClearPreActivationInstance();

		var services = new ServiceCollection();
		services.AddElasticOpenTelemetry(new ElasticOpenTelemetryOptions { SkipOtlpExporter = true });

		// Components are not disposed as other tests may share them via the shared components cache.
		var components = (ElasticOpenTelemetryComponents)services
			.Single(d => d.ServiceType == typeof(ElasticOpenTelemetryComponents)).ImplementationInstance!;

		try
		{
			// Different options, as with a repeated AddElasticOpenTelemetry(IConfiguration) call. Bootstrap
			// will adopt the service collection components, so their logger must be used up front.
			var differentOptions = CreateOpAmpOptions(new CountingLogger());

			var logger = SignalBuilder.GetLogger(null, differentOptions, services);

			Assert.Same(components.Logger, logger);
		}
		finally
		{
			CompositeLogger.ClearPreActivationInstance();
		}
	}

	[Fact]
	public void GetLogger_ReusesSharedComponentsLogger_WhenOptionsMatch()
	{
		CompositeLogger.ClearPreActivationInstance();

		var services = new ServiceCollection();
		services.AddElasticOpenTelemetry(new ElasticOpenTelemetryOptions { SkipOtlpExporter = true });

		// Components are not disposed as other tests may share them via the shared components cache.
		var components = (ElasticOpenTelemetryComponents)services
			.Single(d => d.ServiceType == typeof(ElasticOpenTelemetryComponents)).ImplementationInstance!;

		try
		{
			// No service collection, as with WithElasticDefaults(IConfiguration) on a standalone provider builder.
			var logger = SignalBuilder.GetLogger(null, components.Options, null);

			Assert.Same(components.Logger, logger);
		}
		finally
		{
			CompositeLogger.ClearPreActivationInstance();
		}
	}
}
