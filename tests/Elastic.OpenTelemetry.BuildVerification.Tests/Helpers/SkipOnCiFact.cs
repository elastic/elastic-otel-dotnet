// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

namespace Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

/// <summary>
/// A <see cref="FactAttribute"/> that skips when running in CI.
/// </summary>
public sealed class SkipOnCiFact : FactAttribute
{
	public SkipOnCiFact(string reason = "Temporarily skipped on CI.")
	{
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
			Skip = reason;
	}
}
