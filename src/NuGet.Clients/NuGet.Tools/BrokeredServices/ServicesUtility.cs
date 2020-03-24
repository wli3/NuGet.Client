// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.ServiceBroker;

namespace NuGetVSExtension.BrokeredServices
{
    internal class ServicesUtility
    {
        internal static BrokeredServiceFactory GetNuGetPackageInstallerFactory()
        {
            return (mk, options, sb, ct) => new ValueTask<object>(new AsyncVSPackageInstallerProxy());
        }
    }
}
