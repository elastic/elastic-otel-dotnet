// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information


// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// ReSharper disable once CheckNamespace
namespace OpenTelemetry;


//This is only temporary while we wait for IOpenTelemetryBuilder to ship in the base OpenTelemetry libraries


/// <summary> An interface for configuring OpenTelemetry inside an <see cref="IServiceCollection"/>. </summary>
public interface IOpenTelemetryBuilder
{
	/// <summary>
	/// Gets the <see cref="IServiceCollection"/> where OpenTelemetry services
	/// are configured.
	/// </summary>
	IServiceCollection Services { get; }
}

/// <summary>
/// Contains methods for extending the <see cref="IOpenTelemetryBuilder"/> interface.
/// </summary>
public static class OpenTelemetryBuilderSdkExtensions
{
    /// <summary>
    /// Registers an action to configure the <see cref="ResourceBuilder"/>s used
    /// by tracing, metrics, and logging.
    /// </summary>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied sequentially.
    /// </remarks>
    /// <param name="configure"><see cref="ResourceBuilder"/> configuration
    /// action.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder ConfigureResource(
        this IOpenTelemetryBuilder builder,
        Action<ResourceBuilder> configure)
    {
        builder.Services.ConfigureOpenTelemetryMeterProvider(builder => builder.ConfigureResource(configure));

        builder.Services.ConfigureOpenTelemetryTracerProvider(builder => builder.ConfigureResource(configure));

        //builder.Services.ConfigureOpenTelemetryLoggerProvider(builder => builder.ConfigureResource(configure));

        return builder;
    }

    /// <summary>
    /// Adds metric services into the builder.
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
    public static IOpenTelemetryBuilder WithMetrics(this IOpenTelemetryBuilder builder)
        => WithMetrics(builder, b => { });

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithMetrics(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configure"><see cref="MeterProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithMetrics(
        this IOpenTelemetryBuilder builder,
        Action<MeterProviderBuilder> configure)
	{
		//internal temporary hack while we wait for IOpenTelemetryBuilder to ship
		//TODO cache
		var x = Type.GetType("Microsoft.Extensions.Diagnostics.Metrics.OpenTelemetryMetricsBuilderExtensions");
		var method = x?.GetMethod("RegisterMetricsListener");

		method?.Invoke(null, [builder.Services, configure]);
		return builder;

		/*
        OpenTelemetryMetricsBuilderExtensions.RegisterMetricsListener(
            builder.Services,
            configure);

        return builder;
        */
    }

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithTracing(this IOpenTelemetryBuilder builder)
        => WithTracing(builder, b => { });

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithTracing(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configure"><see cref="TracerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithTracing(
        this IOpenTelemetryBuilder builder,
        Action<TracerProviderBuilder> configure)
	{

		//internal temporary hack while we wait for IOpenTelemetryBuilder to ship
		//TODO cache

		var constructor = typeof(TracerProviderBuilderBase).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
		var value = Expression.Parameter(typeof(IServiceCollection), "services");
		var body = Expression.New(constructor, value);
		var lambda = Expression.Lambda<Func<IServiceCollection, TracerProviderBuilder>>(body, value);
		var tracerProviderBuilder = lambda.Compile()(builder.Services);

        //var tracerProviderBuilder = new TracerProviderBuilderBase(builder.Services);

        configure(tracerProviderBuilder);

        return builder;
    }

}
