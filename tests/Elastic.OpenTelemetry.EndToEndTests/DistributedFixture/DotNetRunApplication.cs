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

	public static readonly DirectoryInfo Root = GetSolutionRoot();
	public static readonly DirectoryInfo LogDirectory = new(Path.Combine(Root.FullName, ".artifacts", "tests"));

	private readonly LongRunningApplicationSubscription _app;
	private readonly string _applicationName;
	private readonly string _authorization;
	private readonly string _endpoint;
	private readonly string _serviceName;

	protected DotNetRunApplication(string serviceName, IConfiguration configuration, string applicationName)
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
		var project = Path.Combine(Root.FullName, "examples", _applicationName);

		var arguments = new[] { "run", "--project", project };
		var applicationArguments = GetArguments();
		if (applicationArguments.Length > 0)
			arguments = [.. arguments, "--", .. applicationArguments];

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

				{ "ELASTIC_OTEL_ENABLE_FILE_LOGGING", "1" },
				{ "ELASTIC_OTEL_LOG_DIRECTORY", LogDirectory.FullName },
				{ "ELASTIC_OTEL_LOG_LEVEL", "INFO" },
			},
			StartedConfirmationHandler = l =>
			{
				//Grab actual process id to send SIGINT to.
				if (l.Line == null)
					return false;
				var processIdMatch = ProcessIdMatch.Match(l.Line);
				if (processIdMatch.Success)
					ProcessId = int.Parse(processIdMatch.Groups["processid"].Value);

				return l.Line.StartsWith("      Application started.");
			}
		};


	}

	public void IterateOverLog(Action<string> write)
	{
		var logFile = DotNetRunApplication.LogDirectory
			 //TODO get last of this app specifically
			 //.GetFiles($"{_app.Process.Binary}_*.log")
			 .GetFiles($"*.log")
			 .MaxBy(f => f.CreationTimeUtc);

		if (logFile == null)
			write($"Could not locate log files in {DotNetRunApplication.LogDirectory}");
		else
		{
			write($"Contents of: {logFile.FullName}");
			using var sr = logFile.OpenText();
			var s = string.Empty;
			while ((s = sr.ReadLine()) != null)
				write(s);
		}
	}

	public virtual void Dispose()
	{
		if (ProcessId.HasValue)
			_app.SendControlC(ProcessId.Value);
		_app.Dispose();
	}
}
