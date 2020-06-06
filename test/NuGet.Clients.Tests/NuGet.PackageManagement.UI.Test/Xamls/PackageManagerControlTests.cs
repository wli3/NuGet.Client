// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI.Test
{
    using Microsoft.VisualStudio.Sdk.TestFramework;

    [Collection(MockedVS.Collection)]
    public class PackageManagerControlTests
    {
        //        private JoinableTaskContext _joinableTaskContext;

        //        public PackageManagerControlTests()
        //        {
        //#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
        //            _joinableTaskContext = new JoinableTaskContext(Thread.CurrentThread, SynchronizationContext.Current);
        //#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

        //            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(_joinableTaskContext.Factory);
        //        }

        public PackageManagerControlTests(GlobalServiceProvider sp)
        {
            sp.Reset();
        }

        [WpfFact]
        public async Task SearchPackagesAndRefreshUpdateCountAsync_IfCancelled_Throws()
        {
            var ui = Mock.Of<INuGetUI>();

            var uiContext = Mock.Of<INuGetUIContext>();
            Mock.Get(uiContext).Setup(p => p.SolutionManager).Returns(Mock.Of<IVsSolutionManager>());
            Mock.Get(uiContext).Setup(p => p.Projects).Returns(Mock.Of<List<NuGetProject>>());
            Mock.Get(uiContext).Setup(p => p.PackageManagerProviders).Returns(Mock.Of<IEnumerable<IVsPackageManagerProvider>>());
            Mock.Get(ui).Setup(p => p.UIContext).Returns(uiContext);
            

            PackageManagerModel vm = new PackageManagerModel(ui, false, Guid.Empty);
            

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var pmControl = new PackageManagerControl(vm, Mock.Of<ISettings>(), Mock.Of<IVsWindowSearchHostFactory>(), Mock.Of<IVsShell4>());
            
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () =>
                {
                    pmControl._loadCts.Cancel();

                    await pmControl.SearchPackagesAndRefreshUpdateCountAsync(searchText: null,
                        useCacheForUpdates: false,
                        pSearchCallback: null,
                        searchTask: null);
                });
        }
    }
}
