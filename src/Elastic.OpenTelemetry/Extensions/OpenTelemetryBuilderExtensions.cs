// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Exporters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Matching namespace with OpenTelemetryBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="IOpenTelemetryBuilder"/>
/// used to register the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class OpenTelemetryBuilderExtensions
{
	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code across all <see cref="IOpenTelemetryBuilder"/> instances. This allows us to warn about potential
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	/// <summary>
	/// Enables collection of all signals using Elastic Distribution of OpenTelemetry .NET defaults.
	/// </summary>
	/// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> being configured.</param>
	/// <returns>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// The supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, CompositeElasticOpenTelemetryOptions.DefaultOptions);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> from which to attempt binding of Elastic Distribution of OpenTelemetry
	/// (EDOT) options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)"/></returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return WithElasticDefaultsCore(builder, new(configuration));
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options">
	/// An <see cref="ElasticOpenTelemetryOptions"/> instance used to configure the initial Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
	/// Note that only the first use for a given <see cref="IOpenTelemetryBuilder"/> instance applies the options. Subsequent builder methods may
	/// accept <see cref="ElasticOpenTelemetryOptions"/> but those will not be reapplied.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return WithElasticDefaultsCore(builder, new(options));
	}

	internal static IOpenTelemetryBuilder WithElasticDefaultsCore(
		this IOpenTelemetryBuilder builder,
		CompositeElasticOpenTelemetryOptions options)
	{
		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		var providerBuilderName = builder.GetType().Name;

		var logger = SignalBuilder.GetLogger(builder, null, options, null);

		if (callCount > 1)
		{
			logger.LogMultipleWithElasticDefaultsCallsWarning(callCount, nameof(IOpenTelemetryBuilder));
		}

		// If for some reason `WithElasticDefaults` is invoked with the `Signals` option set to
		// none, we skip bootstrapping entirely. We log this as a warning since it's best to
		// simply not call `WithElasticDefaults` in this scenario and it may indicate a misconfiguration.
		if (options.Signals == Signals.None)
		{
			logger.LogSkippingBootstrapWarning();
			return builder;
		}

		return SignalBuilder.WithElasticDefaults(builder, options, null, builder.Services, ConfigureBuilder);
	}

	private static void ConfigureBuilder(IOpenTelemetryBuilder builder, BuilderState builderState, IServiceCollection? services)
	{
		var components = builderState.Components;
		var options = builderState.Components.Options;

		services?.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions);

		if (options.Signals.HasFlagFast(Signals.Traces))
		{
			builder.WithTracing(b => b.WithElasticDefaults(components, builder.Services));
		}
		else
		{
			components.Logger.LogSignalDisabled(Signals.Traces.ToString().ToLower(),
				nameof(IOpenTelemetryBuilder), builderState.InstanceIdentifier);
		}

		if (options.Signals.HasFlagFast(Signals.Metrics))
		{
			builder.WithMetrics(b => b.WithElasticDefaults(components, builder.Services));
		}
		else
		{
			components.Logger.LogSignalDisabled(Signals.Metrics.ToString().ToLower(),
				nameof(IOpenTelemetryBuilder), builderState.InstanceIdentifier);
		}

		if (options.Signals.HasFlagFast(Signals.Logs))
		{
			builder.WithLogging(b => b.WithElasticDefaults(components, builder.Services));
		}
		else
		{
			components.Logger.LogSignalDisabled(Signals.Logs.ToString().ToLower(),
				nameof(IOpenTelemetryBuilder), builderState.InstanceIdentifier);
		}
	}

	/// <summary>
	/// Adds metric services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET defaults.
	/// </summary>
	/// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
	/// <remarks>
	/// Notes:
	/// <list type="bullet">
	/// <item>This is safe to be called multiple times and by library authors.
	/// Only a single <see cref="LoggerProvider"/> will be created for a given
	/// <see cref="IServiceCollection"/>.</item>
	/// <item>This method automatically registers an <see
	/// cref="ILoggerProvider"/> named 'OpenTelemetry' into the <see
	/// cref="IServiceCollection"/>.</item>
	/// </list>
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.</returns>
	public static IOpenTelemetryBuilder WithElasticLogging(this IOpenTelemetryBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return builder.WithLogging(lpb => lpb.WithElasticDefaults());
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure"><see cref="LoggerProviderBuilder"/> configuration callback.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticLogging(this IOpenTelemetryBuilder builder, Action<LoggerProviderBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithLogging(lpb =>
			{
				lpb.WithElasticDefaults();
				configure?.Invoke(lpb);
			});
	}

	/// <summary>
	/// Adds metric services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
	/// </summary>
	/// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
	/// <remarks>
	/// Notes:
	/// <list type="bullet">
	/// <item>This is safe to be called multiple times and by library authors.
	/// Only a single <see cref="MeterProvider"/> will be created for a given
	/// <see cref="IServiceCollection"/>.</item>
	/// <item>This method automatically registers an <see
	/// cref="IMetricsListener"/> named 'OpenTelemetry' into the <see
	/// cref="IServiceCollection"/>.</item>
	/// </list>
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.</returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return builder.WithMetrics(mpb => mpb.WithElasticDefaults());
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return builder.WithMetrics(mpb => mpb.WithElasticDefaults(configuration, builder.Services));
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure"><see cref="MeterProviderBuilder"/> configuration callback.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception> 
	/// <returns><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, Action<MeterProviderBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithMetrics(mpb =>
			{
				mpb.WithElasticDefaults(builder.Services);
				configure?.Invoke(mpb);
			});
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">
	/// <inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder, IConfiguration)" path="/param[@name='configuration']"/>
	/// </param>
	/// <param name="configure">
	/// <inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder, Action{MeterProviderBuilder})" path="/param[@name='configure']"/>
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, IConfiguration configuration,
		Action<MeterProviderBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithMetrics(mpb =>
			{
				mpb.WithElasticDefaults(configuration, builder.Services);
				configure?.Invoke(mpb);
			});
	}

	/// <summary>
	/// Adds tracing services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
	/// </summary>
	/// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
	/// <remarks>
	/// Note: This is safe to be called multiple times and by library authors.
	/// Only a single <see cref="TracerProvider"/> will be created for a given
	/// <see cref="IServiceCollection"/>.
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.</returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return builder.WithTracing(m => m.WithElasticDefaults());
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return builder.WithTracing(tpb => tpb.WithElasticDefaults(configuration, builder.Services));
	}

	/// <summary>
	/// Adds tracing services into the builder using Elastic defaults.
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure"><see cref="TracerProviderBuilder"/> configuration callback.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, Action<TracerProviderBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithTracing(tpb =>
			{
				tpb.WithElasticDefaults();
				configure?.Invoke(tpb);
			});
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">
	/// <inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder, IConfiguration)" path="/param[@name='configuration']"/>
	/// </param>
	/// <param name="configure">
	/// <inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder, Action{TracerProviderBuilder})" path="/param[@name='configure']"/>
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configure"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, IConfiguration configuration,
		Action<TracerProviderBuilder> configure)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithTracing(tpb =>
		{
			tpb.WithElasticDefaults(configuration, builder.Services);
			configure?.Invoke(tpb);
		});
	}
}
