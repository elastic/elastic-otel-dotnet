// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

#if NETSTANDARD2_0
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	/// <summary>
	///     Reserved to be used by the compiler for tracking metadata.
	///     This class should not be used by developers in source code.
	/// </summary>
	/// <remarks>
	///     This definition is provided by the <i>IsExternalInit</i> NuGet package (https://www.nuget.org/packages/IsExternalInit).
	///     Please see https://github.com/manuelroemer/IsExternalInit for more information.
	/// </remarks>
	[ExcludeFromCodeCoverage, DebuggerNonUserCode]
	internal static class IsExternalInit;
}
#endif
