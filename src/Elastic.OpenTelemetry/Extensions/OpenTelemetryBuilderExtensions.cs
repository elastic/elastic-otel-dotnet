// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Matching namespace with OpenTelemetryBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods on <see cref="IOpenTelemetryBuilder"/> and <see cref="OpenTelemetryBuilder"/>
/// used to register the Elastic Distribution of OpenTelemetry (EDOT) defaults.
/// </summary>
public static class OpenTelemetryBuilderExtensions
{
	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code, to allow us to warn about potenital misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCounter;

	/// <summary>
	/// Enables collection of all signals using Elastic Distribution of OpenTelemetry .NET defaults.
	/// </summary>
	/// <param name="builder">The <see cref="IOpenTelemetryBuilder"/> being configured.</param>
	/// <returns>
	/// The supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.
	/// </returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder) =>
		WithElasticDefaultsCore(builder, CompositeElasticOpenTelemetryOptions.DefaultOptions);

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> from which to attempt binding of Elastic Distribution of OpenTelemetry
	/// (EDOT) options.</param>
	/// <returns><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)"/></returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configuration);
#else
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
	/// An <see cref="ElasticOpenTelemetryOptions"/> instance used to configure the initial Elastic Distribution of OpenTelemetry (EDOT) defaults.
	/// Note that only the first use for a given <see cref="IOpenTelemetryBuilder"/> instance applies the options. Subsequent builder methods may
	/// accept <see cref="ElasticOpenTelemetryOptions"/> but those will not be reapplied.
	/// </param>
	/// <returns><inheritdoc cref="WithElasticDefaults(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticDefaults(this IOpenTelemetryBuilder builder, ElasticOpenTelemetryOptions options)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(options);
#else
		if (options is null)
			throw new ArgumentNullException(nameof(options));
#endif

		return WithElasticDefaultsCore(builder, new(options));
	}

	internal static IOpenTelemetryBuilder WithElasticDefaultsCore(
		this IOpenTelemetryBuilder builder,
		CompositeElasticOpenTelemetryOptions options)
	{
		var usingExistingState = true; // Will be set to false, if we later create state for this builder.

		// Attempt to load existing state if any Elastic extension methods on this builder have been called
		// previously. This allows reuse of existing components, and ensures we bootstrap once per builder.
		// If the builder is linked to an existing IServiceCollection, then we boostrapping will return the
		// same bootstrapped components, ensuring we also bootstrap once per IServiceCollection.

		// We assign state to each instance of IOpenTelemetryBuilder, so that we can shortcut access to
		// components on subsequent calls where we know bootstrapping has occurred. It also enables us to
		// warn (via logs) when the same method on the same instance is invoked more than once, which is
		// likely to be a user error.
		var builderState = ElasticOpenTelemetry.BuilderStateTable.GetValue(builder, builder =>
		{
			var bootstrapInfo = ElasticOpenTelemetry.TryBootstrap(options, ((IOpenTelemetryBuilder)builder).Services, out var components);
			var builderState = new BuilderState(bootstrapInfo, components);
			usingExistingState = false;
			return builderState;
		});

		builderState.IncrementUseElasticDefaults();

		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCounter);

		if (builderState.UseElasticDefaultsCounter > 1)
		{
			// TODO - Log warning
		}
		else if (callCount > 1)
		{
			// TODO - Log warning
		}

		if (!usingExistingState)
		{
			// TODO - Log
		}

		var bootstrapInfo = builderState.BootstrapInfo;
		var components = builderState.Components;

		Debug.Assert(bootstrapInfo is not null, "BootstrapInfo should not be null after successful bootstrap.");
		Debug.Assert(components is not null, "Components should not be null after successful bootstrap.");

		if (!bootstrapInfo.Succeeded)
		{
			options?.AdditionalLogger?.LogError("Unable to bootstrap EDOT.");
			ElasticOpenTelemetry.BuilderStateTable.Remove(builder);
			return builder;
		}

		builder.WithLogging(b => b.UseElasticDefaults(components, builder.Services));
		builder.WithMetrics(b => b.UseElasticDefaults(components, builder.Services));
		builder.WithTracing(b => b.UseElasticDefaults(components, builder.Services));

		return builder;
	}

	/// <summary>
	/// Adds metric services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of OpenTelemetry (EDOT) defaults.
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
	/// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
	/// calls.</returns>
	public static IOpenTelemetryBuilder WithElasticLogging(this IOpenTelemetryBuilder builder) =>
		builder.WithLogging(lpb => lpb.UseElasticDefaults());

	/// <summary>
	/// <inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure"><see cref="LoggerProviderBuilder"/> configuration callback.</param>
	/// <returns><inheritdoc cref="WithElasticLogging(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticLogging(this IOpenTelemetryBuilder builder, Action<LoggerProviderBuilder> configure)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithLogging(lpb =>
			{
				lpb.UseElasticDefaults();
				configure?.Invoke(lpb);
			});
	}

	/// <summary>
	/// Adds metric services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of OpenTelemetry (EDOT) defaults.
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
	/// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
	/// calls.</returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder) =>
		builder.WithMetrics(mpb => mpb.UseElasticDefaults());

	/// <summary>
	/// <inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) options.</param>
	/// <returns><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return builder.WithMetrics(mpb => mpb.UseElasticDefaults(configuration, builder.Services));
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure"><see cref="MeterProviderBuilder"/> configuration callback.</param>
	/// <returns><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, Action<MeterProviderBuilder> configure)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithMetrics(mpb =>
			{
				mpb.UseElasticDefaults(builder.Services);
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
	/// <returns><inheritdoc cref="WithElasticMetrics(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticMetrics(this IOpenTelemetryBuilder builder, IConfiguration configuration,
		Action<MeterProviderBuilder> configure)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithMetrics(mpb =>
			{
				mpb.UseElasticDefaults(configuration, builder.Services);
				configure?.Invoke(mpb);
			});
	}

	/// <summary>
	/// Adds tracing services into the <see cref="IOpenTelemetryBuilder"/> using Elastic Distribution of OpenTelemetry (EDOT) defaults.
	/// </summary>
	/// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
	/// <remarks>
	/// Note: This is safe to be called multiple times and by library authors.
	/// Only a single <see cref="TracerProvider"/> will be created for a given
	/// <see cref="IServiceCollection"/>.
	/// </remarks>
	/// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.</returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder) =>
		builder.WithTracing(m => m.UseElasticDefaults());

	/// <summary>
	/// <inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" />
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) options.</param>
	/// <returns><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, IConfiguration configuration)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configuration);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));
#endif

		return builder.WithTracing(tpb => tpb.UseElasticDefaults(configuration, builder.Services));
	}

	/// <summary>
	/// Adds tracing services into the builder using Elastic defaults.
	/// </summary>
	/// <remarks><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configure"><see cref="TracerProviderBuilder"/> configuration callback.</param>
	/// <returns><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, Action<TracerProviderBuilder> configure)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithTracing(tpb =>
			{
				tpb.UseElasticDefaults();
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
	/// <returns><inheritdoc cref="WithElasticTracing(IOpenTelemetryBuilder)" /></returns>
	public static IOpenTelemetryBuilder WithElasticTracing(this IOpenTelemetryBuilder builder, IConfiguration configuration,
		Action<TracerProviderBuilder> configure)
	{
#if NET8_0_OR_GREATER
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configure);
#else
		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configure is null)
			throw new ArgumentNullException(nameof(configure));
#endif

		return builder.WithTracing(tpb =>
		{
			tpb.UseElasticDefaults(configuration, builder.Services);
			configure?.Invoke(tpb);
		});
	}
}
