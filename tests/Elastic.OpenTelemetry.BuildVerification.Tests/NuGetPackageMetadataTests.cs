// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO.Compression;
using System.Xml.Linq;
using Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

namespace Elastic.OpenTelemetry.BuildVerification.Tests;

/// <summary>
/// Packs the NuGet packages and verifies the .nuspec dependency metadata
/// is correct — ensuring consumers get the right transitive dependencies.
/// </summary>
[Collection("BuildArtifacts")]
public class NuGetPackageMetadataTests(PackOutputFixture fixture)
{
	[Fact]
	public void ElasticOpenTelemetry_Package_IncludesOpAmpClientDependency()
	{
		var dependencies = GetNuspecDependencies("Elastic.OpenTelemetry");

		Assert.Contains(dependencies, d =>
			d.Id.Equals("OpenTelemetry.OpAmp.Client", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void AutoInstrumentation_Package_IncludesOpAmpClientDependency()
	{
		var dependencies = GetNuspecDependencies("Elastic.OpenTelemetry.AutoInstrumentation");

		Assert.Contains(dependencies, d =>
			d.Id.Equals("OpenTelemetry.OpAmp.Client", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void AutoInstrumentation_Package_ExcludesPrivateAssetsDependencies()
	{
		var dependencies = GetNuspecDependencies("Elastic.OpenTelemetry.AutoInstrumentation");

		// OpenTelemetry.Exporter.OpenTelemetryProtocol has PrivateAssets="all"
		// so it should NOT appear in the package dependencies
		Assert.DoesNotContain(dependencies, d =>
			d.Id.Equals("OpenTelemetry.Exporter.OpenTelemetryProtocol", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void ElasticOpenTelemetry_Package_IncludesProtobufDependency()
	{
		var dependencies = GetNuspecDependencies("Elastic.OpenTelemetry");
		var expectedVersion = GetCpmVersion("Google.Protobuf");

		var protobuf = dependencies.FirstOrDefault(d =>
			d.Id.Equals("Google.Protobuf", StringComparison.OrdinalIgnoreCase));

		Assert.True(protobuf is not null,
			"Google.Protobuf should be a direct dependency (check PackageReference and Directory.Packages.props)");
		Assert.Equal(expectedVersion, protobuf!.Version);
	}

	[Fact]
	public void AutoInstrumentation_Package_IncludesProtobufDependency()
	{
		var dependencies = GetNuspecDependencies("Elastic.OpenTelemetry.AutoInstrumentation");
		var expectedVersion = GetCpmVersion("Google.Protobuf");

		var protobuf = dependencies.FirstOrDefault(d =>
			d.Id.Equals("Google.Protobuf", StringComparison.OrdinalIgnoreCase));

		Assert.True(protobuf is not null,
			"Google.Protobuf should be a direct dependency (check PackageReference and Directory.Packages.props)");
		Assert.Equal(expectedVersion, protobuf!.Version);
	}

	/// <summary>
	/// Reads the CPM-pinned version from Directory.Packages.props.
	/// Assumes the file uses no default XML namespace (standard MSBuild convention).
	/// </summary>
	private static string GetCpmVersion(string packageId)
	{
		var propsPath = Path.Combine(DotNetHelper.SolutionRoot, "Directory.Packages.props");
		var doc = XDocument.Load(propsPath);
		var ns = doc.Root!.Name.Namespace;

		var entry = doc.Descendants(ns + "PackageVersion")
			.FirstOrDefault(e =>
				e.Attribute("Include")?.Value
					.Equals(packageId, StringComparison.OrdinalIgnoreCase) == true);

		Assert.NotNull(entry);
		var version = entry.Attribute("Version")?.Value;
		Assert.NotNull(version);
		return version;
	}

	private List<NuspecDependency> GetNuspecDependencies(string packageId)
	{
		// Filter precisely: packageId followed by a version digit, excluding .snupkg
		var nupkgFiles = Directory.GetFiles(fixture.PackOutputDir, "*.nupkg")
			.Where(f => !f.EndsWith(".snupkg"))
			.Where(f => Path.GetFileName(f).StartsWith($"{packageId}.") &&
				// Ensure the character after "packageId." is a digit (version start)
				// to avoid "Elastic.OpenTelemetry." matching "Elastic.OpenTelemetry.AutoInstrumentation."
				Path.GetFileName(f).Length > packageId.Length + 1 &&
				char.IsDigit(Path.GetFileName(f)[packageId.Length + 1]))
			.ToArray();
		Assert.Single(nupkgFiles);

		using var zip = ZipFile.OpenRead(nupkgFiles[0]);
		var nuspecEntry = zip.Entries.First(e => e.Name.EndsWith(".nuspec"));

		using var stream = nuspecEntry.Open();
		var doc = XDocument.Load(stream);
		var ns = doc.Root!.Name.Namespace;

		return doc.Descendants(ns + "dependency")
			.Select(el => new NuspecDependency(
				el.Attribute("id")?.Value ?? string.Empty,
				el.Attribute("version")?.Value ?? string.Empty))
			.ToList();
	}

	private sealed record NuspecDependency(string Id, string Version);
}
