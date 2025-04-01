// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information


using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace Example.WorkerService;

public class QueueReader
{
	public async IAsyncEnumerable<Message> GetMessages([EnumeratorCancellation] CancellationToken ctx = default)
	{
		while (!ctx.IsCancellationRequested)
		{
			// Get messages from queue/service bus
			await Task.Delay(TimeSpan.FromSeconds(5), ctx);

			yield return new Message(Guid.NewGuid().ToString());
		}
	}
}

public record class Message(string Id) { }

public class Worker : BackgroundService
{
	public const string DiagnosticName = "Elastic.Processor";

	private static readonly ActivitySource ActivitySource = new(DiagnosticName);
	private static readonly Meter Meter = new(DiagnosticName);
	private static readonly Counter<int> MessagesReadCounter = Meter.CreateCounter<int>("elastic.processor.messages_read");

	private readonly ILogger<Worker> _logger;
	private readonly QueueReader _queueReader;

	private static readonly Random Random = new();

	public Worker(ILogger<Worker> logger, QueueReader queueReader)
	{
		_logger = logger;
		_queueReader = queueReader;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await foreach (var message in _queueReader.GetMessages().WithCancellation(stoppingToken))
		{
			using var activity = ActivitySource.StartActivity("Process message", ActivityKind.Internal);

			activity?.SetTag("elastic.message.id", message.Id);

			if (MessagesReadCounter.Enabled)
				MessagesReadCounter.Add(1);

			var success = await ProcessMessageAsync(message);

			if (!success)
			{
				_logger.LogError("Unable to process message {Id}", message.Id);
				activity?.SetStatus(ActivityStatusCode.Error);
			}
		}
	}

	private static async Task<bool> ProcessMessageAsync(Message message)
	{
		await Task.Delay(Random.Next(100, 300)); // simulate processing
		return Random.Next(10) < 8; // simulate 80% success
	}
}
