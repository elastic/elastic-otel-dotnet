// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information
using System.Text;

namespace Elastic.OpenTelemetry.Diagnostics;

// SOURCE: https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Text/StringBuilderCache.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
internal static class StringBuilderCache
{
	// The value 360 was chosen in discussion with performance experts as a compromise between using
	// as little memory per thread as possible and still covering a large part of short-lived
	// StringBuilder creations on the startup path of VS designers.
	internal const int MaxBuilderSize = 360;
	private const int DefaultCapacity = 64; // At least as large as the prefix

	[ThreadStatic]
	private static StringBuilder? CachedInstance;

	/// <summary>Get a StringBuilder for the specified capacity.</summary>
	/// <remarks>If a StringBuilder of an appropriate size is cached, it will be returned and the cache emptied.</remarks>
	public static StringBuilder Acquire(int capacity = DefaultCapacity)
	{
		if (capacity <= MaxBuilderSize)
		{
			var sb = CachedInstance;
			if (sb != null)
			{
				// Avoid StringBuilder block fragmentation by getting a new StringBuilder
				// when the requested size is larger than the current capacity
				if (capacity <= sb.Capacity)
				{
					CachedInstance = null;
					sb.Clear();
					return sb;
				}
			}
		}

		return new StringBuilder(capacity);
	}

	/// <summary>Place the specified builder in the cache if it is not too big.</summary>
	public static void Release(StringBuilder sb)
	{
		if (sb.Capacity <= MaxBuilderSize)
			CachedInstance = sb;
	}

	/// <summary>ToString() the StringBuilder, Release it to the cache, and return the resulting string.</summary>
	public static string GetStringAndRelease(StringBuilder sb)
	{
		var result = sb.ToString();
		Release(sb);
		return result;
	}
}
