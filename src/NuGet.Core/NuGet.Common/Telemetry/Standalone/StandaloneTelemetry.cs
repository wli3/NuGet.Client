// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    public static class StandaloneTelemetry
    {
        public static bool Start()
        {
            if (TelemetryActivity.NuGetTelemetryService == null)
            {
                TelemetryActivity.NuGetTelemetryService = new StandaloneTelemetryService();
                return true;
            }

            return false;
        }

        public static void Stop()
        {
            StandaloneTelemetrySession.StopTelementry();
        }
    }
}
