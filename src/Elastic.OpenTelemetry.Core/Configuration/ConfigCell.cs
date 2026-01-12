// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.Configuration;

internal class ConfigCell<T>
{
	internal ConfigCell(string key, T value)
	{
		Key = key;
		Value = value;
	}

	internal ConfigCell(string key, T value, Func<T, T> valueRedactor)
	{
		Key = key;
		Value = value;
		ValueRedactor = valueRedactor;
	}

	internal string Key { get; }

	internal Func<T, T> ValueRedactor { get; } = original => original;

	internal T Value { get; private set; }

	internal ConfigSource Source { get; private set; } = ConfigSource.Default;

	internal void AssignFromEnvironmentVariable(T value) =>
		Assign(value, ConfigSource.Environment);

	internal void AssignFromConfiguration(T value) =>
		Assign(value, ConfigSource.IConfiguration);

	internal void AssignFromOptions(T value) =>
		Assign(value, ConfigSource.Options);

	internal void AssignFromProperty(T value) =>
		Assign(value, ConfigSource.Property);

	private void Assign(T value, ConfigSource source)
	{
		Value = value;
		Source = source;
	}

	public override string ToString() => $"{Key}: '{ValueRedactor(Value)}' from [{Source}]";
}
