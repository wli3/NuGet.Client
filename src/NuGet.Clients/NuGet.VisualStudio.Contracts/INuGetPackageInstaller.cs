// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.VisualStudio.Contracts
{
    /// <summary>
    /// Contains methods to install packages into a project within the current solution.
    /// </summary>
    public interface INuGetPackageInstaller
    {
        /// <summary>
        /// Installs a single package from the specified package source. If <paramref name="source"/> is <c>null</c>, the user's configured sources are used!
        /// </summary>
        /// <param name="source">
        /// The package source to install the package from. This value can be <c>null</c>
        /// to indicate that the user's configured sources should be used. Otherwise,
        /// this should be the source path as a string. If the user has credentials
        /// configured for a source, this value must exactly match the configured source
        /// value.
        /// </param>
        /// <param name="projectUniqueName">A unique id for the project for package installation.</param>
        /// <param name="packageId">The package ID of the package to install.</param>
        /// <param name="version">
        /// The version of the package to install. <c>null</c> can be provided to
        /// install the latest version of the package.
        /// </param>
        /// <param name="cancellationToken">CancellationToken for cancelling the operation.</param>
        Task InstallPackageAsync(
            string source,
            string projectUniqueName,
            string packageId,
            string version,
            CancellationToken cancellationToken);

        /// <summary>
        /// Installs the latest version of a single package from the specified package source.
        /// </summary>
        /// <param name="source">
        /// The package source to install the package from. This value can be <c>null</c>
        /// to indicate that the user's configured sources should be used. Otherwise,
        /// this should be the source path as a string. If the user has credentials
        /// configured for a source, this value must exactly match the configured source
        /// value.
        /// </param>
        /// <param name="projectUniqueName">A unique id for the project for package installation.</param>
        /// <param name="packageId">The package ID of the package to install.</param>
        /// <param name="includePrerelease">
        /// Whether or not to consider prerelease versions when finding the latest version
        /// to install.
        /// </param>
        /// <param name="cancellationToken">CancellationToken for cancelling the operation.</param>
        Task InstallLatestPackageAsync(
            string source,
            string projectUniqueName,
            string packageId,
            bool includePrerelease,
            CancellationToken cancellationToken);
    }
}
