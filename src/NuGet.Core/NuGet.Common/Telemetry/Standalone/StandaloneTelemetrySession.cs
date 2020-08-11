// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

using VSTelemetryEvent = Microsoft.VisualStudio.Telemetry.TelemetryEvent;
using TelemetryService = Microsoft.VisualStudio.Telemetry.TelemetryService;
using TelemetryPiiProperty = Microsoft.VisualStudio.Telemetry.TelemetryPiiProperty;
using TelemetryComplexProperty = Microsoft.VisualStudio.Telemetry.TelemetryComplexProperty;

namespace NuGet.Common
{

    internal class StandaloneTelemetrySession : ITelemetrySession
    {
        readonly static object LockInstance = new object();
        static StandaloneTelemetrySession InternalInstance;
        readonly string _executingAssemblyVersion;

        public static StandaloneTelemetrySession Instance()
        {
            if (InternalInstance == null)
            {
                lock (LockInstance)
                {
                    if (InternalInstance == null)
                    {
                        InternalInstance = new StandaloneTelemetrySession();
                    }
                }
            }

            return InternalInstance;
         }

        private const string OutOfVSEventNamePrefix = "VS/NuGet/"; //"VS/NuGet/Standalone/";
        private const string OutOfVSPropertyNamePrefix = "VS.NuGet.";//"VS.NuGet.Standalone.";

        private StandaloneTelemetrySession()
        {
            var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(executingAssembly.Location);
            _executingAssemblyVersion = $"{executingAssembly.GetName().Name}_{fvi.ProductVersion}_{executingAssembly.GetName().Version}";
            StartTelemetry();
        }

        public void PostEvent(TelemetryEvent telemetryEvent)
        {
            TelemetryService.DefaultSession.PostEvent(ToOOVSTelemetryEvent(telemetryEvent));
        }

        public VSTelemetryEvent ToOOVSTelemetryEvent(TelemetryEvent telemetryEvent)
        {
            if (telemetryEvent == null)
            {
                throw new ArgumentNullException(nameof(telemetryEvent));
            }

            var vsTelemetryEvent = new VSTelemetryEvent(OutOfVSEventNamePrefix + telemetryEvent.Name);

            foreach (KeyValuePair<string, object> pair in telemetryEvent)
            {
                vsTelemetryEvent.Properties[OutOfVSPropertyNamePrefix + pair.Key] = pair.Value;
            }

            foreach (KeyValuePair<string, object> pair in telemetryEvent.GetPiiData())
            {
                vsTelemetryEvent.Properties[OutOfVSPropertyNamePrefix + pair.Key] = new TelemetryPiiProperty(pair.Value);
            }

            foreach (KeyValuePair<string, object> pair in telemetryEvent.ComplexData)
            {
                vsTelemetryEvent.Properties[OutOfVSPropertyNamePrefix + pair.Key] = new TelemetryComplexProperty(ToComplexProperty(pair.Value));
            }

            vsTelemetryEvent.Properties["NuGetVersion"] = _executingAssemblyVersion;

            return vsTelemetryEvent;
        }

        private object ToComplexProperty(object value)
        {
            if (value is TelemetryEvent telemetryEvent)
            {
                var dictionary = new Dictionary<string, object>();

                foreach (KeyValuePair<string, object> pair in telemetryEvent)
                {
                    dictionary[pair.Key] = pair.Value;
                }

                foreach (KeyValuePair<string, object> pair in telemetryEvent.GetPiiData())
                {
                    dictionary[pair.Key] = new TelemetryPiiProperty(pair.Value);
                }

                foreach (KeyValuePair<string, object> pair in telemetryEvent.ComplexData)
                {
                    dictionary[pair.Key] = ToComplexProperty(pair.Value);
                }

                return dictionary;
            }
            else if (value is IEnumerable enumerable)
            {
                var list = new List<object>();

                foreach (var item in enumerable)
                {
                    list.Add(ToComplexProperty(item));
                }

                return list;
            }
            else
            {
                return value;
            }
        }

        public static void StopTelementry()
        {
            TelemetryService.DefaultSession.Dispose();
        }

        static void StartTelemetry()
        {
            TelemetryService.DefaultSession.IsOptedIn = true;
            TelemetryService.DefaultSession.UseVsIsOptedIn();
            TelemetryService.DefaultSession.Start();
        }
    }
}
