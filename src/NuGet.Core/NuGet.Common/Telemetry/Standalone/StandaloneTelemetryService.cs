// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Common
{
    internal class StandaloneTelemetryService : INuGetTelemetryService
    {
        private ITelemetrySession _telemetrySession;

        internal StandaloneTelemetryService(ITelemetrySession telemetrySession)
        {
            _telemetrySession = telemetrySession ?? throw new ArgumentNullException(nameof(telemetrySession));
        }

        internal StandaloneTelemetryService() :
            this(StandaloneTelemetrySession.Instance())
        {
        }

        public virtual void EmitTelemetryEvent(TelemetryEvent telemetryData)
        {
            if (telemetryData == null)
            {
                throw new ArgumentNullException(nameof(telemetryData));
            }

            _telemetrySession.PostEvent(telemetryData);
        }

        public virtual IDisposable StartActivity(string activityName)
        {
            return null;
        }
    }
}
