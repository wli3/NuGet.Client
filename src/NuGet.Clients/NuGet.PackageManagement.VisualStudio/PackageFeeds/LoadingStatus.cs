// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// List of possible statuses of items loading operation (search).
    /// Utilized by item loader and UI for progress tracking.
    /// </summary>
    [Flags]
    public enum LoadingStatus
    {
        Unknown = 0, // not initialized
        Cancelled = 1, // loading cancelled
        ErrorOccurred = 2, // error occured
        Loading = 4, // loading is running in background
        NoItemsFound = 8, // loading complete, no items found
        NoMoreItems = 16, // loading complete, no more items discovered beyond current page
        Ready = 32, // loading of current page is done, next page is available

        Failed = Cancelled | ErrorOccurred,
        Completed = NoItemsFound | NoMoreItems
    }
}
