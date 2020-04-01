// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal sealed class NuGetPackageManagerControlSearchTask : IVsSearchTask
    {
        private PackageManagerControl _packageManagerControl;
        private IVsSearchCallback _searchCallback;

        public NuGetPackageManagerControlSearchTask(PackageManagerControl packageManagerControl, uint id, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
        {
            _packageManagerControl = packageManagerControl;
            _searchCallback = pSearchCallback;
            SearchQuery = pSearchQuery;
            Id = id;
            ErrorCode = 0;
            SetStatus(VsSearchTaskStatus.Created);

        }
        public void Start()
        {
            SetStatus(VsSearchTaskStatus.Started);
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                await _packageManagerControl.SearchPackagesAndRefreshUpdateCountAsync(searchInput: SearchQuery.SearchString, useCacheForUpdates: true, pSearchCallback: _searchCallback, searchTask: this);
                SetStatus(VsSearchTaskStatus.Completed);
            });
        }

        public uint Id { get; private set; }

        public IVsSearchQuery SearchQuery { get; set; }

        public uint Status { get; private set; }

        public int ErrorCode { get; private set; }

        public void Stop()
        {
            SetStatus(VsSearchTaskStatus.Stopped);
        }

        private void SetStatus(VsSearchTaskStatus taskStatus)
        {
            Status = (uint)taskStatus;
        }

        private enum VsSearchTaskStatus : uint
        {
            Created = 0,
            Started = 1,
            Completed = 2,
            Stopped = 3,
            Error = 4
        }
    }
}
