// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Matching namespace with TracerProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry.Trace;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="TracerProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
internal static class TracerProvderBuilderExtensions
{
	private static int AddElasticProcessorsCallCount;

	/// <summary>
	/// Include Elastic trace processors for best compatibility with Elastic Observability.
	/// </summary>
	/// <remarks>
	/// <para>It is not neccessary to call this method if `WithElasticDefaults` has already been called.</para>
	/// <para>Calling this method also adds Elastic defaults to the resource builder.</para>
	/// </remarks>
	/// <param name="builder">The <see cref="TracerProviderBuilder"/> where the Elastic trace
	/// processors should be added.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The <see cref="TracerProviderBuilder"/> for chaining.</returns>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return AddElasticProcessorsCore(builder, null, null, null);
	}

	/// <summary>
	/// An advanced API to include Elastic Distribution of OpenTelemtry (EDOT) .NET trace processors for best compatibility with
	/// Elastic Observability. Generally, prefer using `WithElasticDefaults` instead, which registers default trace instrumentation.
	/// </summary>
	/// <remarks>
	/// <para>It is not neccessary to call this method if `WithElasticDefaults` has already been called.</para>
	/// <para>Calling this method also bootstraps the Elastic Distribution of OpenTelemtry (EDOT) .NET for logging and configuration
	/// and adds Elastic defaults to the resource builder.</para>
	/// </remarks>
	/// <param name="builder">The <see cref="TracerProviderBuilder"/> where the Elastic trace
	/// processors should be added.</param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns>The <see cref="TracerProviderBuilder"/> for chaining.</returns>
	public static TracerProviderBuilder AddElasticProcessors(this TracerProviderBuilder builder, ElasticOpenTelemetryOptions options)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return AddElasticProcessorsCore(builder, null, null, null);
	}

	internal static TracerProviderBuilder AddElasticProcessorsCore(
		TracerProviderBuilder builder,
		BuilderState? builderState,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services)
	{
		components ??= builderState?.Components;

		var callCount = Interlocked.Increment(ref AddElasticProcessorsCallCount);

		var logger = SignalBuilder.GetLogger(builder, components, null, builderState);

		if (callCount > 1)
			logger.LogMultipleAddElasticProcessorsCallsWarning(callCount);

		if (builderState is not null)
		{
			// When we have existing builderState, this method is being invoked from the main WithElasticDefaults method.
			// In that scenario, we skip configuring the resource, as it will have already been configured by the caller.
			return builder;
		}

		return SignalBuilder.WithElasticDefaults(builder, Signals.Traces, components?.Options, components, null, ConfigureBuilder);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static void ConfigureBuilder(TracerProviderBuilder builder, BuilderState builderState, IServiceCollection? services)
		{
			builder.ConfigureResource(r => r.WithElasticDefaultsCore(builderState, services, null));
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static TracerProviderBuilder LogAndAddProcessor(this TracerProviderBuilder builder, BaseProcessor<Activity> processor, BuilderState builderState)
	{
		builder.AddProcessor(processor);
		builderState.Components.Logger.LogProcessorAdded(processor.GetType().ToString(), builderState.InstanceIdentifier);
		return builder;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void AddActivitySourceWithLogging(TracerProviderBuilder builder, ILogger logger, string activitySource, string builderIdentifier)
	{
		builder.AddSource(activitySource);
		logger.LogActivitySourceAdded(activitySource, builderIdentifier);
	}
}
