// See https://aka.ms/new-console-template for more information

using System.Collections;

Console.WriteLine("Hello, World!");

foreach (DictionaryEntry kv in Environment.GetEnvironmentVariables())
	Console.WriteLine($"{kv.Key}={kv.Value}");

var httpClient = new HttpClient();
for (var i = 0; i < 10; i++)
{
	var response = await httpClient.GetAsync(new Uri("https://google.com"));
	Console.Write($"\rSent {i + 1} requests, last response: {response.StatusCode}.");
	await Task.Delay(TimeSpan.FromMilliseconds(100));
}
Console.WriteLine();
