// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Reflection;
using System.Text.RegularExpressions;
using ProcNet;

namespace Elastic.OpenTelemetry.IntegrationTests;

public class DistributedApplicationFixture : IDisposable
{
	private readonly AspNetCoreExampleApplication _aspNetApplication;

	public DistributedApplicationFixture() => _aspNetApplication = new AspNetCoreExampleApplication();

	public bool Started => _aspNetApplication.ProcessId.HasValue;

	public void Dispose() => _aspNetApplication.Dispose();
}

public class AspNetCoreExampleApplication : IDisposable
{
	private static readonly Regex ProcessIdMatch = new(@"^\s*Process Id (?<processid>\d+)");
	private readonly LongRunningApplicationSubscription _app;

	public AspNetCoreExampleApplication()
	{
		var args = CreateStartArgs();
		_app = Proc.StartLongRunning(args, TimeSpan.FromSeconds(10));
	}

	public int? ProcessId { get; private set; }

	private LongRunningArguments CreateStartArgs()
	{
		var start = new FileInfo(Assembly.GetExecutingAssembly().Location);
		var root = start.Directory;
		while (root != null && root.GetFiles("*.sln").Length == 0)
			root = root.Parent;

		if (root == null)
			throw new Exception($"Could not locate root starting from {start}");

		var project = Path.Combine(root.FullName, "examples", "Example.Elastic.OpenTelemetry.AspNetCore");

		return new("dotnet", "run", "--project", project)
		{
			StartedConfirmationHandler = (l) =>
			{
				var processIdMatch = ProcessIdMatch.Match(l.Line);
				if (processIdMatch.Success)
					ProcessId = int.Parse(processIdMatch.Groups["processid"].Value);

				return l.Line.StartsWith("      Application started.");
			}
		};
	}

	public void Dispose()
	{
		var pid = _app.Process.ProcessId;
		if (ProcessId.HasValue)
			_app.SendControlC(ProcessId.Value);
		_app.Dispose();
	}
}
