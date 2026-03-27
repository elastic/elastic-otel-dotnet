// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.OpenTelemetry.Configuration;

namespace Elastic.OpenTelemetry.Tests.Configuration;

public class ConfigCellPrecedenceTests
{
	private static ConfigCell<string> CreateCell(string key = "test", string? initial = null) =>
		new(key, initial);

	// --- Precedence enforcement: higher source wins ---

	[Fact]
	public void CentralConfig_OverridesEnvironment()
	{
		var cell = CreateCell();
		cell.AssignFromEnvironmentVariable("env");
		cell.AssignFromCentralConfig("central");

		Assert.Equal("central", cell.Value);
		Assert.Equal(ConfigSource.CentralConfig, cell.Source);
	}

	[Fact]
	public void CentralConfig_OverridesProperty()
	{
		var cell = CreateCell();
		cell.AssignFromProperty("prop");
		cell.AssignFromCentralConfig("central");

		Assert.Equal("central", cell.Value);
		Assert.Equal(ConfigSource.CentralConfig, cell.Source);
	}

	[Fact]
	public void CentralConfig_OverridesOptions()
	{
		var cell = CreateCell();
		cell.AssignFromOptions("opts");
		cell.AssignFromCentralConfig("central");

		Assert.Equal("central", cell.Value);
		Assert.Equal(ConfigSource.CentralConfig, cell.Source);
	}

	[Fact]
	public void CentralConfig_OverridesIConfiguration()
	{
		var cell = CreateCell();
		cell.AssignFromConfiguration("iconfig");
		cell.AssignFromCentralConfig("central");

		Assert.Equal("central", cell.Value);
		Assert.Equal(ConfigSource.CentralConfig, cell.Source);
	}

	[Fact]
	public void Property_OverridesOptions()
	{
		var cell = CreateCell();
		cell.AssignFromOptions("opts");
		cell.AssignFromProperty("prop");

		Assert.Equal("prop", cell.Value);
		Assert.Equal(ConfigSource.Property, cell.Source);
	}

	[Fact]
	public void Options_OverridesEnvironment()
	{
		var cell = CreateCell();
		cell.AssignFromEnvironmentVariable("env");
		cell.AssignFromOptions("opts");

		Assert.Equal("opts", cell.Value);
		Assert.Equal(ConfigSource.Options, cell.Source);
	}

	[Fact]
	public void Environment_OverridesIConfiguration()
	{
		var cell = CreateCell();
		cell.AssignFromConfiguration("iconfig");
		cell.AssignFromEnvironmentVariable("env");

		Assert.Equal("env", cell.Value);
		Assert.Equal(ConfigSource.Environment, cell.Source);
	}

	// --- Precedence rejection: lower source cannot overwrite higher ---

	[Fact]
	public void Environment_DoesNotOverrideOptions()
	{
		var cell = CreateCell();
		cell.AssignFromOptions("opts");
		cell.AssignFromEnvironmentVariable("env");

		Assert.Equal("opts", cell.Value);
		Assert.Equal(ConfigSource.Options, cell.Source);
	}

	[Fact]
	public void IConfiguration_DoesNotOverrideEnvironment()
	{
		var cell = CreateCell();
		cell.AssignFromEnvironmentVariable("env");
		cell.AssignFromConfiguration("iconfig");

		Assert.Equal("env", cell.Value);
		Assert.Equal(ConfigSource.Environment, cell.Source);
	}

	[Fact]
	public void Options_DoesNotOverrideCentralConfig()
	{
		var cell = CreateCell();
		cell.AssignFromCentralConfig("central");
		cell.AssignFromOptions("opts");

		Assert.Equal("central", cell.Value);
		Assert.Equal(ConfigSource.CentralConfig, cell.Source);
	}

	[Fact]
	public void Property_DoesNotOverrideCentralConfig()
	{
		var cell = CreateCell();
		cell.AssignFromCentralConfig("central");
		cell.AssignFromProperty("prop");

		Assert.Equal("central", cell.Value);
		Assert.Equal(ConfigSource.CentralConfig, cell.Source);
	}

	[Fact]
	public void Default_NeverOverridesAnything()
	{
		// There is no AssignFromDefault method, so Default can only be the initial state.
		// Verify that any non-Default assignment is never reverted to Default.
		var cell = CreateCell();
		Assert.Equal(ConfigSource.Default, cell.Source);

		cell.AssignFromConfiguration("iconfig");
		Assert.Equal(ConfigSource.IConfiguration, cell.Source);

		// No mechanism exists to go back to Default — validate by construction.
	}

	// --- Same-source updates ---

	[Fact]
	public void SameSource_CentralConfig_LastWriteWins()
	{
		var cell = CreateCell();
		cell.AssignFromCentralConfig("A");
		cell.AssignFromCentralConfig("B");

		Assert.Equal("B", cell.Value);
		Assert.Equal(ConfigSource.CentralConfig, cell.Source);
	}

	[Fact]
	public void SameSource_Environment_LastWriteWins()
	{
		var cell = CreateCell();
		cell.AssignFromEnvironmentVariable("A");
		cell.AssignFromEnvironmentVariable("B");

		Assert.Equal("B", cell.Value);
		Assert.Equal(ConfigSource.Environment, cell.Source);
	}

	// --- Snapshot consistency ---

	[Fact]
	public void Snapshot_ReturnsConsistentPair()
	{
		var cell = CreateCell();
		cell.AssignFromOptions("opts");

		var (value, source) = cell.Snapshot();

		Assert.Equal("opts", value);
		Assert.Equal(ConfigSource.Options, source);
	}

	// --- Thread safety ---

	[Fact]
	public async Task ConcurrentAssign_HighestPrecedenceWins()
	{
		const int iterations = 10_000;

		for (var i = 0; i < iterations; i++)
		{
			var cell = CreateCell();
			var barrier = new Barrier(3);

			var t1 = Task.Run(() =>
			{
				barrier.SignalAndWait();
				cell.AssignFromEnvironmentVariable("env");
			});
			var t2 = Task.Run(() =>
			{
				barrier.SignalAndWait();
				cell.AssignFromOptions("opts");
			});
			var t3 = Task.Run(() =>
			{
				barrier.SignalAndWait();
				cell.AssignFromCentralConfig("central");
			});

			await Task.WhenAll(t1, t2, t3);

			Assert.Equal("central", cell.Value);
			Assert.Equal(ConfigSource.CentralConfig, cell.Source);
		}
	}

	[Fact]
	public async Task Snapshot_IsConsistentUnderConcurrentWrites()
	{
		// Valid (value, source) pairs — a torn read would produce a mismatched pair
		var validPairs = new Dictionary<ConfigSource, string>
		{
			{ ConfigSource.Default, "<null>" }, // sentinel for null value
			{ ConfigSource.Environment, "env" },
			{ ConfigSource.Options, "opts" },
			{ ConfigSource.CentralConfig, "central" }
		};

		const int iterations = 10_000;
		var inconsistencies = 0;

		for (var i = 0; i < iterations; i++)
		{
			var cell = CreateCell();
			var barrier = new Barrier(4); // 3 writers + 1 reader

			var reader = Task.Run(() =>
			{
				barrier.SignalAndWait();

				// Take multiple snapshots during the race window
				for (var j = 0; j < 100; j++)
				{
					var (value, source) = cell.Snapshot();
					var actual = value ?? "<null>";

					if (!validPairs.TryGetValue(source, out var expected) || actual != expected)
						Interlocked.Increment(ref inconsistencies);
				}
			});

			var t1 = Task.Run(() =>
			{
				barrier.SignalAndWait();
				cell.AssignFromEnvironmentVariable("env");
			});
			var t2 = Task.Run(() =>
			{
				barrier.SignalAndWait();
				cell.AssignFromOptions("opts");
			});
			var t3 = Task.Run(() =>
			{
				barrier.SignalAndWait();
				cell.AssignFromCentralConfig("central");
			});

			await Task.WhenAll(reader, t1, t2, t3);
		}

		Assert.Equal(0, inconsistencies);
	}
}
