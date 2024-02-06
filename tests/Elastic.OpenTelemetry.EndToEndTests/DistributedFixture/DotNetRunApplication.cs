// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using ProcNet;

namespace Elastic.OpenTelemetry.EndToEndTests.DistributedFixture;

public abstract class DotNetRunApplication
{
	private static readonly DirectoryInfo CurrentDirectory = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory!;
	private static readonly Regex ProcessIdMatch = new(@"^\s*Process Id (?<processid>\d+)");
	private readonly LongRunningApplicationSubscription _app;
	private readonly string _applicationName;
	private readonly string _authorization;
	private readonly string _endpoint;
	private readonly string _serviceName;

	public DotNetRunApplication(string serviceName, IConfiguration configuration, string applicationName)
	{
		_serviceName = serviceName;
		_applicationName = applicationName;
		_endpoint = configuration["E2E:Endpoint"]?.Trim() ?? string.Empty;
		_authorization = configuration["E2E:Authorization"]?.Trim() ?? string.Empty;

		var args = CreateStartArgs();
		_app = Proc.StartLongRunning(args, TimeSpan.FromSeconds(10));
	}

	public int? ProcessId { get; private set; }

	protected virtual string[] GetArguments() => Array.Empty<string>();

	public static DirectoryInfo GetSolutionRoot()
	{
		var root = CurrentDirectory;
		while (root != null && root.GetFiles("*.sln").Length == 0)
			root = root.Parent;

		if (root == null)
			throw new Exception($"Could not locate root starting from {CurrentDirectory}");

		return root;
	}

	private LongRunningArguments CreateStartArgs()
	{
		var root = GetSolutionRoot();
		var project = Path.Combine(root.FullName, "examples", _applicationName);

		var arguments = new[] { "run", "--project", project };
		var applicationArguments = GetArguments();
		if (applicationArguments.Length > 0)
			arguments = [..arguments, "--", ..applicationArguments];

		return new("dotnet", arguments)
		{
			Environment = new Dictionary<string, string>
			{
				{ "OTEL_EXPORTER_OTLP_ENDPOINT", _endpoint },
				{ "OTEL_EXPORTER_OTLP_HEADERS", _authorization },
				{ "OTEL_METRICS_EXPORTER", "otlp" },
				{ "OTEL_LOGS_EXPORTER", "otlp" },
				{ "OTEL_BSP_SCHEDULE_DELAY", "1000" },
				{ "OTEL_BSP_MAX_EXPORT_BATCH_SIZE", "5" },
				{ "OTEL_RESOURCE_ATTRIBUTES", $"service.name={_serviceName},service.version=1.0,1,deployment.environment=e2e" },
			},
			StartedConfirmationHandler = (l) =>
			{
				//Grab actual process id to send SIGINT to.
				var processIdMatch = ProcessIdMatch.Match(l.Line);
				if (processIdMatch.Success)
					ProcessId = int.Parse(processIdMatch.Groups["processid"].Value);

				return l.Line.StartsWith("      Application started.");
			}
		};
	}

	public virtual void Dispose()
	{
		var pid = _app.Process.ProcessId;
		if (ProcessId.HasValue)
			_app.SendControlC(ProcessId.Value);
		_app.Dispose();
	}
}
