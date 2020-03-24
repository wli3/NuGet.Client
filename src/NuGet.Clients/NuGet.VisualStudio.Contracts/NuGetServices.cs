// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ServiceHub.Framework;
using NuGet.Shared;
using NuGet.VisualStudio.Contracts;

namespace Microsoft.VisualStudio
{
    /// <summary>
    /// A static class containing descriptors and extension method for the NuGet Services the contracts represent.
    /// </summary>
    public static class NuGetServices
    {
        /// <summary>
        /// A service descriptor for the PackageInstaller service. 
        /// </summary>
        public static ServiceRpcDescriptor PackageInstallerService { get; } = new ServiceJsonRpcDescriptor(
          new ServiceMoniker(ServicesIdentityHelper.PackageInstallerServiceName, new Version(ServicesIdentityHelper.PackageInstallerServiceVersion)),
          ServiceJsonRpcDescriptor.Formatters.UTF8,
          ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);

        /// <summary> 
        /// Gets the <see cref="ServiceRpcDescriptor"/> for the IVsPackageInstaller service. 
        /// Use the <see cref="INuGetPackageInstaller"/> interface for the client proxy for this service. 
        /// </summary> 
        public static ServiceRpcDescriptor PackageInstaller(this VisualStudioServices.VS2019_5Services svc) => PackageInstallerService;
    }
}
