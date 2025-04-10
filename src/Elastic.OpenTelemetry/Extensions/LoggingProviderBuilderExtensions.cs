// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;
using Elastic.OpenTelemetry;
using Elastic.OpenTelemetry.Configuration;
using Elastic.OpenTelemetry.Core;
using Elastic.OpenTelemetry.Diagnostics;
using Elastic.OpenTelemetry.Exporters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

// Matching namespace with LoggerProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="LoggerProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET defaults.
/// </summary>
public static class LoggingProviderBuilderExtensions
{
	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code across all <see cref="LoggerProviderBuilder"/> instances. This allows us to warn about potential
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	/// <summary>
	/// Use Elastic Distribution of OpenTelemetry (EDOT) .NET defaults for <see cref="LoggerProviderBuilder"/>.
	/// </summary>
	/// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>The <see cref="LoggerProviderBuilder"/> for chaining configuration.</returns>
	public static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, null, null, null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" /></returns>
	public static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder, ElasticOpenTelemetryOptions options)
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

		return WithElasticDefaultsCore(builder, new(options), null, null);
	}

	/// <summary>
	/// <inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" />
	/// </summary>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the OpenTelemetry SDK options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" /></returns>
	public static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder, IConfiguration configuration)
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
		return WithElasticDefaultsCore(builder, new(configuration), null, null);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder, ElasticOpenTelemetryComponents components, IServiceCollection? services) =>
		WithElasticDefaultsCore(builder, components.Options, components, services);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static LoggerProviderBuilder WithElasticDefaultsCore(
		this LoggerProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services)
	{
		var logger = SignalBuilder.GetLogger(builder, components, options, null);

		var callCount = Interlocked.Increment(ref WithElasticDefaultsCallCount);

		if (callCount > 1)
		{
			logger.LogMultipleWithElasticDefaultsCallsWarning(callCount, nameof(LoggerProviderBuilder));
		}
		else
		{
			logger.LogWithElasticDefaultsCallCount(callCount, nameof(LoggerProviderBuilder));
		}

		return SignalBuilder.WithElasticDefaults(builder, Signals.Traces, options, components, services, ConfigureBuilder);

		static void ConfigureBuilder(LoggerProviderBuilder builder, BuilderState builderState, IServiceCollection? services)
		{
			const string loggingProviderName = nameof(LoggerProviderBuilder);
			var components = builderState.Components;
			var logger = components.Logger;

			logger.LogConfiguringBuilder(loggingProviderName, builderState.InstanceIdentifier);

			builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

			// When services is not null here, the options will have already been configured by the calling code.
			if (services is null)
				builder.ConfigureServices(sc => sc.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions));

			builder.ConfigureServices(sc => sc.Configure<OpenTelemetryLoggerOptions>(o => o.WithElasticDefaults(logger)));

			if (builder is IDeferredLoggerProviderBuilder deferredBuilder)
			{
				var httpContextType = Type.GetType("Microsoft.AspNetCore.Http.HttpContext, Microsoft.AspNetCore.Http.Abstractions");

				if (httpContextType is not null)
				{
					var options = deferredBuilder.Configure((sp, _) =>
					{
						var options = sp.GetService<IOptions<OpenTelemetryLoggerOptions>>();

						if (options is not null && options.Value.IncludeScopes == true)
						{
							logger.LogDetectedIncludeScopesWarning();
						}
					});
				}
			}

			if (components.Options.SkipOtlpExporter)
			{
				logger.LogSkippingOtlpExporter(nameof(Signals.Logs), loggingProviderName, builderState.InstanceIdentifier);
			}
			else
			{
				builder.AddOtlpExporter();
			}

			logger.LogConfiguredSignalProvider(nameof(Signals.Logs), loggingProviderName, builderState.InstanceIdentifier);
		}
	}
}
