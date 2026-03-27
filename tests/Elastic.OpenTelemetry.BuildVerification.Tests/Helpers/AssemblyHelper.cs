// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Elastic.OpenTelemetry.BuildVerification.Tests.Helpers;

internal static class AssemblyHelper
{
	/// <summary>
	/// Checks whether an assembly contains a type definition with the given name.
	/// Uses PE metadata reader — does not load the assembly into the runtime.
	/// </summary>
	internal static bool ContainsType(string assemblyPath, string typeName)
	{
		using var stream = File.OpenRead(assemblyPath);
		using var peReader = new PEReader(stream);
		var metadataReader = peReader.GetMetadataReader();

		foreach (var typeDefHandle in metadataReader.TypeDefinitions)
		{
			var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
			var name = metadataReader.GetString(typeDef.Name);
			if (name == typeName)
				return true;
		}
		return false;
	}
}
