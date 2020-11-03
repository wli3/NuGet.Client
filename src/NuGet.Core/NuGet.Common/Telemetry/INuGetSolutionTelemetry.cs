// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common.Telemetry
{
    // This one used for emitting aggregated telemetry at VS solution close or VS instance close.
    public interface INuGetSolutionTelemetry
    {
        /// <summary> Send a <see cref="TelemetryEvent"/> to telemetry. </summary>
        /// <param name="telemetryData"> Telemetry event to send. </param>
        void AddSolutionTelemetryEvent(TelemetryEvent telemetryData);
    }
}
