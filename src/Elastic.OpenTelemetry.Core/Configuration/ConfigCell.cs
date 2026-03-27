// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Threading;

namespace Elastic.OpenTelemetry.Configuration;

internal class ConfigCell<T>
{
	private readonly Lock _lock = new();

	internal ConfigCell(string key, T? value)
	{
		Key = key;
		Value = value;
	}

	internal ConfigCell(string key, T? value, Func<T?, T?> valueRedactor)
	{
		Key = key;
		Value = value;
		ValueRedactor = valueRedactor;
	}

	internal string Key { get; }

	internal Func<T?, T?> ValueRedactor { get; } = original => original;

	internal T? Value { get; private set; }

	internal ConfigSource Source { get; private set; } = ConfigSource.Default;

	internal void AssignFromEnvironmentVariable(T value) =>
		Assign(value, ConfigSource.Environment);

	internal void AssignFromConfiguration(T value) =>
		Assign(value, ConfigSource.IConfiguration);

	internal void AssignFromOptions(T value) =>
		Assign(value, ConfigSource.Options);

	internal void AssignFromProperty(T value) =>
		Assign(value, ConfigSource.Property);

	internal void AssignFromCentralConfig(T value) =>
		Assign(value, ConfigSource.CentralConfig);

	/// <summary>
	/// Sets the value and source. A lower-precedence source cannot overwrite a
	/// higher-precedence one — precedence is defined by <see cref="ConfigSource"/> ordinal.
	/// Uses strict less-than so that same-source updates (e.g. successive CentralConfig
	/// pushes) are permitted.
	/// </summary>
	private void Assign(T value, ConfigSource source)
	{
		lock (_lock)
		{
			if (source < Source)
				return;

			Value = value;
			Source = source;
		}
	}

	/// <summary>
	/// Returns a consistent snapshot of both <see cref="Value"/> and <see cref="Source"/>.
	/// </summary>
	internal (T? Value, ConfigSource Source) Snapshot()
	{
		lock (_lock)
			return (Value, Source);
	}

	public override string ToString()
	{
		var (value, source) = Snapshot();
		return $"{Key}: '{(value == null ? "<null>" : ValueRedactor(value))}' from [{source}]";
	}
}
