using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Microsoft.Internal.VisualStudio.PlatformUI.Automation;

namespace NuGet.PackageManagement.UI
{
    internal class InfiniteScrollListAutomationPeer : FrameworkElementAutomationPeer, ICustomAutomationEventSource
    {
        public InfiniteScrollListAutomationPeer(InfiniteScrollList owner)
            : base(owner)
        {
        }

        public IRawElementProviderSimple GetProvider()
        {
            return ProviderFromPeer(this);
        }
    }
}
