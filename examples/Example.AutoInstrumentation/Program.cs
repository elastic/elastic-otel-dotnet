// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

Console.WriteLine("Hello, World!");

var httpClient = new HttpClient();
for (var i = 0; i < 10; i++)
{
	var response = await httpClient.GetAsync(new Uri("https://google.com"));
	Console.Write($"\rSent {i + 1} requests, last response: {response.StatusCode}.");
	await Task.Delay(TimeSpan.FromMilliseconds(100));
}
Console.WriteLine();
