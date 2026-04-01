// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO.Compression;
using Xunit.Abstractions;

namespace Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

/// <summary>
/// Extracts the redistributable zip once and shares the extracted directory
/// across all <see cref="ZipResolverVerificationTests"/>. Prevents redundant
/// extractions and reduces Windows file-lock cleanup issues from ALC-loaded
/// assemblies.
/// </summary>
public class ZipExtractionFixture : IDisposable
{
	private readonly IMessageSink _diagnosticSink;

	public string ExtractDir { get; }

	/// <summary>Path to the extracted net/ subfolder, or null if the zip has no net/ folder.</summary>
	public string? NetDir { get; }

	/// <summary>Whether a redistributable zip was found in the artifacts.</summary>
	public bool ZipFound { get; }

	public ZipExtractionFixture(IMessageSink diagnosticSink)
	{
		_diagnosticSink = diagnosticSink;
		ExtractDir = Path.Combine(Path.GetTempPath(), $"edot-zip-resolver-{Guid.NewGuid():N}");
		Directory.CreateDirectory(ExtractDir);

		var distroDir = Path.Combine(DotNetHelper.SolutionRoot, ".artifacts", "elastic-distribution");
		if (!Directory.Exists(distroDir))
		{
			Log("No elastic-distribution directory found — tests will be skipped");
			return;
		}

		// Find any redistributable zip (prefer Linux for cross-platform CI)
		var zip = Directory.GetFiles(distroDir, "*.zip")
			.FirstOrDefault(f => !f.EndsWith("-windows.zip"))
			?? Directory.GetFiles(distroDir, "*.zip").FirstOrDefault();

		if (zip is null)
		{
			Log("No redistributable zip found — tests will be skipped");
			return;
		}

		ZipFound = true;
		Log($"Extracting {Path.GetFileName(zip)} to {ExtractDir}");
		ZipFile.ExtractToDirectory(zip, ExtractDir);

		var netDir = Path.Combine(ExtractDir, "net");
		NetDir = Directory.Exists(netDir) ? netDir : null;
		Log($"Extraction complete — net/ folder {(NetDir is not null ? "found" : "MISSING")}");
	}

	public void Dispose()
	{
		// Best-effort cleanup. ALC-loaded assemblies are memory-mapped by the CLR,
		// so file locks may persist even after unloading collectible ALCs. The OS
		// temp directory handles eventual cleanup.
		try
		{
			if (Directory.Exists(ExtractDir))
				Directory.Delete(ExtractDir, true);
		}
		catch (Exception) when (OperatingSystem.IsWindows())
		{
			// Expected on Windows — ALC file locks on extracted assemblies.
			Log("Cleanup skipped (Windows file locks on ALC-loaded assemblies)");
		}
	}

	private void Log(string message) =>
		_diagnosticSink.OnMessage(new Xunit.Sdk.DiagnosticMessage($"[ZipFixture] {message}"));
}
