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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Matching namespace with LoggerProviderBuilder
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace OpenTelemetry;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on the <see cref="LoggerProviderBuilder"/> used to register
/// the Elastic Distribution of OpenTelemetry (EDOT) .NET <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>
/// for logging.
/// </summary>
public static class LoggerProviderBuilderExtensions
{
	/// <summary>
	/// Used to track the number of times any variation of `WithElasticDefaults` is invoked by consuming
	/// code across all <see cref="LoggerProviderBuilder"/> instances. This allows us to warn about potential
	/// misconfigurations.
	/// </summary>
	private static int WithElasticDefaultsCallCount;

	/// <summary>
	/// Apply Elastic Distribution of OpenTelemetry (EDOT) .NET
	/// <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">defaults</see>
	/// to the <see cref="LoggerProviderBuilder"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. For example, <see cref="HostApplicationBuilderExtensions.AddElasticOpenTelemetry(IHostApplicationBuilder, Action{IOpenTelemetryBuilder})"/>
	///   This ensures that your configuration is invoked after EDOT .NET defaults have been applied, but before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="LoggerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder">The <see cref="LoggerProviderBuilder"/> to configure.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="LoggerProviderBuilder"/> that can be used to further configure the logging signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="LoggerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{LoggerProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
	public static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));
#endif

		return WithElasticDefaultsCore(builder, null, null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. For example, <see cref="HostApplicationBuilderExtensions.AddElasticOpenTelemetry(IHostApplicationBuilder, Action{IOpenTelemetryBuilder})"/>
	///   This ensures that your configuration is invoked after EDOT .NET defaults have been applied, but before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="LoggerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <paramref name="configureBuilder"/> action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configureBuilder">
	/// An <see cref="Action"/> used to further configure the <see cref="LoggerProviderBuilder"/>.
	/// This action is invoked after <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/setup/edot-defaults">EDOT .NET defaults</see>
	/// have been applied, but before the OTLP exporter is added. This ensures that any custom processors run before the exporter.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns>
	/// <para>
	///   An instance of <see cref="LoggerProviderBuilder"/> that can be used to further configure the logging signal.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="LoggerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <paramref name="configureBuilder"/> action to customise
	///   the <see cref="LoggerProviderBuilder"/> after EDOT .NET defaults are applied and before the OTLP exporter is added.
	/// </para>
	/// </returns>
	public static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder, Action<LoggerProviderBuilder> configureBuilder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<LoggerProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, null, null, null, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. For example,
	///   <see cref="HostApplicationBuilderExtensions.AddElasticOpenTelemetry(IHostApplicationBuilder, ElasticOpenTelemetryOptions, Action{IOpenTelemetryBuilder})"/>
	///   This ensures that your configuration is invoked after EDOT .NET defaults have been applied, but before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="LoggerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(LoggerProviderBuilder, ElasticOpenTelemetryOptions, Action{LoggerProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><see cref="ElasticOpenTelemetryOptions"/> used to configure the Elastic Distribution of OpenTelemetry (EDOT) .NET.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="LoggerProviderBuilder"/> that can be used to further configure the logging signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="LoggerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(LoggerProviderBuilder, ElasticOpenTelemetryOptions, Action{LoggerProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{LoggerProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
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

		return WithElasticDefaultsCore(builder, new(options), null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="options"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, ElasticOpenTelemetryOptions)" path="/param[@name='options']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})" /></returns>
	public static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder,
		ElasticOpenTelemetryOptions options, Action<LoggerProviderBuilder> configureBuilder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (options is null)
			throw new ArgumentNullException(nameof(options));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<LoggerProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, new(options), null, null, builderOptions);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" /></summary>
	/// <remarks>
	/// <para>
	///   This method is intended for advanced scenarios where per signal Elastic Distribution of OpenTelemetry (EDOT) .NET
	///   defaults are being enabled. In most scenarios, prefer the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>.
	/// </para>
	/// <para>
	///   <b>Warning:</b> Avoid calling this method when using the <c>AddElasticOpenTelemetry</c> method on <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/>. In those scenarios, the OpenTelemetry SDK will have already been configured with
	///   defaults, including the OTLP exporter. Calling this method again may lead to unexpected behavior.
	/// </para>
	/// <para>
	///   When using <c>AddElasticOpenTelemetry</c>, use the overload on the <see cref="IHostApplicationBuilder"/>
	///   or <see cref="IServiceCollection"/> which accepts a configuration <see cref="Action{IOpenTelemetryBuilder}"/>
	///   to further customize the SDK setup. For example, <see cref="HostApplicationBuilderExtensions.AddElasticOpenTelemetry(IHostApplicationBuilder, Action{IOpenTelemetryBuilder})"/>
	///   This ensures that your configuration is invoked after EDOT .NET defaults have been applied, but before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   <b>Note:</b> If you wish to customise the <see cref="LoggerProviderBuilder"/> after the EDOT .NET defaults have been applied,
	///   use the <see cref="WithElasticDefaults(LoggerProviderBuilder, IConfiguration, Action{LoggerProviderBuilder})"/> overload that accepts a
	///   configuration action.
	/// </para>
	/// </remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration">An <see cref="IConfiguration"/> instance from which to load the Elastic Distribution of
	/// OpenTelemetry (EDOT) .NET options.</param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <returns>
	/// An instance of <see cref="LoggerProviderBuilder"/> that can be used to further configure the logging signal.
	/// <para>
	///   <b>Warning:</b> Any further configuration applied via the returned <see cref="LoggerProviderBuilder"/> will
	///   be applied <em>after</em> the OTLP exporter has been added.
	/// </para>
	/// <para>
	///   <b>Recommendation:</b>It is strongly recommended to use the <see cref="WithElasticDefaults(LoggerProviderBuilder, IConfiguration, Action{LoggerProviderBuilder})"/>
	///   overload instead. This accepts an <see cref="Action{LoggerProviderBuilder}"/> configuration callback and ensures any
	///   customisations are applied before the OTLP exporter is added.
	/// </para>
	/// <para>
	///   Alternatively, the <see cref="ElasticOpenTelemetryOptions.SkipOtlpExporter"/>
	///   <see href="https://www.elastic.co/docs/reference/opentelemetry/edot-sdks/dotnet/configuration#skipotlpexporter">option</see>
	///   automatic addition of the OTLP exporter, allowing you to add it manually at a later stage.
	/// </para>
	/// </returns>
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
		return WithElasticDefaultsCore(builder, new(configuration), null, null, default);
	}

	/// <summary><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" /></summary>
	/// <remarks><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})" /></remarks>
	/// <param name="builder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder)" path="/param[@name='builder']"/></param>
	/// <param name="configuration"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, IConfiguration)" path="/param[@name='options']"/></param>
	/// <param name="configureBuilder"><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})" path="/param[@name='configureBuilder']"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configuration"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="configureBuilder"/> is null.</exception>
	/// <returns><inheritdoc cref="WithElasticDefaults(LoggerProviderBuilder, Action{LoggerProviderBuilder})" /></returns>
	public static LoggerProviderBuilder WithElasticDefaults(this LoggerProviderBuilder builder,
		IConfiguration configuration, Action<LoggerProviderBuilder> configureBuilder)
	{
#if NET
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(configureBuilder);
#else
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (configuration is null)
			throw new ArgumentNullException(nameof(configuration));

		if (configureBuilder is null)
			throw new ArgumentNullException(nameof(configureBuilder));
#endif
		var builderOptions = new BuilderOptions<LoggerProviderBuilder> { UserProvidedConfigureBuilder = configureBuilder };
		return WithElasticDefaultsCore(builder, new(configuration), null, null, builderOptions);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static LoggerProviderBuilder WithElasticDefaultsCore(
		this LoggerProviderBuilder builder,
		CompositeElasticOpenTelemetryOptions? options,
		ElasticOpenTelemetryComponents? components,
		IServiceCollection? services,
		in BuilderOptions<LoggerProviderBuilder> builderOptions)
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

		return SignalBuilder.WithElasticDefaults(builder, options, components, services, builderOptions, ConfigureBuilder);

		static void ConfigureBuilder(BuilderContext<LoggerProviderBuilder> builderContext)
		{
			var builder = builderContext.Builder;
			var builderState = builderContext.BuilderState;
			var components = builderState.Components;
			var logger = components.Logger;
			var services = builderContext.Services;

			// FullName may return null so we fallback to Name when required.
			var loggingProviderBuilderName = builder.GetType().FullName ?? builder.GetType().Name;

			logger.LogConfiguringBuilder(loggingProviderBuilderName, builderState.InstanceIdentifier);

			builder.ConfigureResource(r => r.WithElasticDefaults(builderState, services));

			// When services is not null here, the options will have already been configured by the calling code so
			// we don't need to do it again.
			if (services is null)
			{
				builder.ConfigureServices(sc => sc.Configure<OtlpExporterOptions>(OtlpExporterDefaults.OtlpExporterOptions));
				logger.LogConfiguredOtlpExporterOptions();
			}

			builder.ConfigureServices(sc => sc.Configure<OpenTelemetryLoggerOptions>(o => o.WithElasticDefaults(logger)));

			// This check is to detect if ASP.NET Core is present in the application.
			// If it is, we check if IncludeScopes is enabled and log a warning because the upstream OTLP exporter
			// exports duplicate attributes which does not conform to the spec and breaks the EDOT Collector.
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

			// Invoke any user-provided configuration.
			var userProvidedConfigureBuilder = builderContext.BuilderOptions.UserProvidedConfigureBuilder;
			if (userProvidedConfigureBuilder is not null)
			{
				userProvidedConfigureBuilder(builder);
				logger.LogInvokedConfigureAction(loggingProviderBuilderName, builderState.InstanceIdentifier);
			}

			if (builderContext.BuilderOptions.DeferAddOtlpExporter)
			{
				// If a callee will register the OTLP exporter later, we skip adding it here.
				logger.LogDeferredOtlpExporter(loggingProviderBuilderName, builderState.InstanceIdentifier);
			}
			else
			{
				if (components.Options.SkipOtlpExporter)
				{
					logger.LogSkippedOtlpExporter(nameof(Signals.Logs), loggingProviderBuilderName, builderState.InstanceIdentifier);
				}
				else
				{
					builder.AddOtlpExporter();
					logger.LogAddedOtlpExporter(nameof(Signals.Logs), loggingProviderBuilderName, builderState.InstanceIdentifier);
				}
			}

			logger.LogConfiguredSignalProvider(nameof(Signals.Logs), loggingProviderBuilderName, builderState.InstanceIdentifier);
		}
	}
}
