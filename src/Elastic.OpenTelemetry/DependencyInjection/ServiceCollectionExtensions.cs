using Elastic.OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection
{
	/// <summary>
	/// Extension methods for <see cref="IServiceProvider"/>.
	/// </summary>
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Adds the Elastic OpenTelemetry distribution to an application via the <see cref="IServiceCollection"/>.
		/// </summary>
		/// <param name="serviceCollection">TODO</param>
		/// <returns>TODO</returns>
		public static IServiceCollection AddOtelElasticAgent(this IServiceCollection serviceCollection) =>
			new AgentBuilder().Build(serviceCollection);

		/// <summary>
		/// TODO
		/// </summary>
		/// <param name="serviceCollection"></param>
		/// <param name="activitySourceNames"></param>
		/// <returns></returns>
		public static IServiceCollection AddOtelElasticAgent(this IServiceCollection serviceCollection, params string[] activitySourceNames) =>
			new AgentBuilder(activitySourceNames).Build(serviceCollection);

		/// <summary>
		/// TODO
		/// </summary>
		/// <param name="serviceCollection"></param>
		/// <param name="configureTracerProvider"></param>
		/// <returns></returns>
		public static IServiceCollection AddOtelElasticAgent(this IServiceCollection serviceCollection, Action<TracerProviderBuilder> configureTracerProvider) =>
			new AgentBuilder().ConfigureTracer(configureTracerProvider).Build(serviceCollection);
	}
}
