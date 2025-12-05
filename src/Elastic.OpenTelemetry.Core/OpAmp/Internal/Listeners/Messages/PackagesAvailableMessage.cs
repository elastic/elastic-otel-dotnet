// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

#pragma warning disable IDE0021 // Use expression body for constructor
#pragma warning disable IDE0003 // Remove qualification

internal class PackagesAvailableMessage : IOpAmpMessage
{
    public PackagesAvailableMessage(PackagesAvailable packageAvailable)
    {
		this.PackagesAvailable = packageAvailable;
	}

    public PackagesAvailable PackagesAvailable { get; set; }
}
