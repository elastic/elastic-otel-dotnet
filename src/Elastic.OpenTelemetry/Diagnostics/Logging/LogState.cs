// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Collections;
using System.Diagnostics;

namespace Elastic.OpenTelemetry.Diagnostics.Logging;

internal class LogState : IReadOnlyList<KeyValuePair<string, object?>>
{
	private readonly Activity? _activity;
	public Activity? Activity
	{
		get => _activity;
		init
		{
			_values.Add(new KeyValuePair<string, object?>(nameof(Activity), value));
			_activity = value;
		}
	}

	private readonly int _managedThreadId;

	public int ManagedThreadId
	{
		get => _managedThreadId;
		init => _managedThreadId = value;
	}

	private readonly DateTime _dateTime;

	public DateTime DateTime
	{
		get => _dateTime;
		init => _dateTime = value;
	}

	private readonly string? _spanId;

	public string? SpanId
	{
		get => _spanId;
		init => _spanId = value;
	}

	private readonly List<KeyValuePair<string, object?>> _values = new ();

	public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

	public int Count => _values.Count;

	public KeyValuePair<string, object?> this[int index] => _values[index];
}
