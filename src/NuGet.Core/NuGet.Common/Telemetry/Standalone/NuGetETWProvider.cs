
using System;
using System.Diagnostics.Tracing;

namespace NuGet.Common.ETW
{
    [EventSource(Name = "NuGetETWProvider")]
    public class NuGetETWProvider : EventSource
    {
        private static Lazy<NuGetETWProvider> LazyInstance = new Lazy<NuGetETWProvider>(() => new NuGetETWProvider());

        private NuGetETWProvider()
        {
        }

        internal static NuGetETWProvider Instance
        {
            get
            {
                return LazyInstance.Value;
            }
        }

        public void WriteEventDuration(string Name, string DurationMilliseconds)
        {
            if (IsEnabled())
            {
                WriteEvent(1, Name, DurationMilliseconds);
            }
        }

        public void WriteFullEventData(string Name, string jsonEventString)
        {
            if (IsEnabled())
            {
                WriteEvent(2, Name, jsonEventString);
            }
        }

#if Debug
        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            var m = command.Arguments.Count;
        }
#endif

    }
}
