// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using static NuGet.Protocol.Core.Types.PackageSearchMetadataBuilder;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class PackageSearchMetadataEqualityComparer : IEqualityComparer<IPackageSearchMetadata>
    {
        public bool IgnoreVersion { get; private set; }
        public PackageSearchMetadataEqualityComparer(bool ignoreVersion)
        {
            IgnoreVersion = ignoreVersion;
        }
        public bool Equals(IPackageSearchMetadata x, IPackageSearchMetadata y)
        {
            if (x == null || y == null || x.GetType() != y.GetType())
            {
                return false;
            }

            switch (x)
            {
                case LocalPackageSearchMetadata feed1:
                    return LocalPackageSearchMetadataEquals(feed1, y as LocalPackageSearchMetadata);
                case PackageSearchMetadataRegistration feed1:
                    return PackageSearchMetadataRegistrationEquals(feed1, y as PackageSearchMetadataRegistration);
                case PackageSearchMetadata feed1:
                    return PackageSearchMetadataEquals(feed1, y as PackageSearchMetadata);
                case ClonedPackageSearchMetadata feed1:
                    return ClonedPackageSearchMetadataEquals(feed1, y as ClonedPackageSearchMetadata);
                case PackageSearchMetadataV2Feed feed1:
                    return PackageSearchMetadataV2FeedEquals(feed1, y as PackageSearchMetadataV2Feed);
                default:
                    return false;
            }
        }

        private bool LocalPackageSearchMetadataEquals(LocalPackageSearchMetadata feed1, LocalPackageSearchMetadata feed2)
        {
            return feed1.Authors == feed2.Authors &&
                   EqualityComparer<IEnumerable<PackageDependencyGroup>>.Default.Equals(feed1.DependencySets, feed2.DependencySets) &&
                   feed1.Description == feed2.Description &&
                   feed1.DownloadCount == feed2.DownloadCount &&
                   EqualityComparer<Uri>.Default.Equals(feed1.IconUrl, feed2.IconUrl) &&
                   (IgnoreVersion ? feed1.Identity.Id.Equals(feed2.Identity.Id) : EqualityComparer<PackageIdentity>.Default.Equals(feed1.Identity, feed2.Identity)) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.LicenseUrl, feed2.LicenseUrl) &&
                   feed1.Owners == feed2.Owners &&
                   EqualityComparer<Uri>.Default.Equals(feed1.ProjectUrl, feed2.ProjectUrl) &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(feed1.Published, feed2.Published) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.ReportAbuseUrl, feed2.ReportAbuseUrl) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.PackageDetailsUrl, feed2.PackageDetailsUrl) &&
                   feed1.RequireLicenseAcceptance == feed2.RequireLicenseAcceptance &&
                   feed1.Summary == feed2.Summary &&
                   feed1.Tags == feed2.Tags &&
                   feed1.Title == feed2.Title &&
                   feed1.IsListed == feed2.IsListed &&
                   feed1.PrefixReserved == feed2.PrefixReserved &&

                   EqualityComparer<Func<PackageReaderBase>>.Default.Equals(feed1.PackageReader, feed2.PackageReader) &&
                   //EqualityComparer<NuspecReader>.Default.Equals(feed1._nuspec, feed2._nuspec) &&
                   //EqualityComparer<LocalPackageInfo>.Default.Equals(feed1._package, feed2._package) &&
                   EqualityComparer<LicenseMetadata>.Default.Equals(feed1.LicenseMetadata, feed2.LicenseMetadata);
        }

        private bool PackageSearchMetadataEquals(PackageSearchMetadata feed1, PackageSearchMetadata feed2)
        {
            return feed1.Authors == feed2.Authors &&
                   EqualityComparer<IEnumerable<PackageDependencyGroup>>.Default.Equals(feed1.DependencySets, feed2.DependencySets) &&
                   feed1.Description == feed2.Description &&
                   feed1.DownloadCount == feed2.DownloadCount &&
                   EqualityComparer<Uri>.Default.Equals(feed1.IconUrl, feed2.IconUrl) &&
                   (IgnoreVersion ? feed1.Identity.Id.Equals(feed2.Identity.Id) : EqualityComparer<PackageIdentity>.Default.Equals(feed1.Identity, feed2.Identity)) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.LicenseUrl, feed2.LicenseUrl) &&
                   feed1.Owners == feed2.Owners &&
                   EqualityComparer<Uri>.Default.Equals(feed1.ProjectUrl, feed2.ProjectUrl) &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(feed1.Published, feed2.Published) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.ReportAbuseUrl, feed2.ReportAbuseUrl) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.PackageDetailsUrl, feed2.PackageDetailsUrl) &&
                   feed1.RequireLicenseAcceptance == feed2.RequireLicenseAcceptance &&
                   feed1.Summary == feed2.Summary &&
                   feed1.Tags == feed2.Tags &&
                   feed1.Title == feed2.Title &&
                   feed1.IsListed == feed2.IsListed &&
                   feed1.PrefixReserved == feed2.PrefixReserved &&

                   feed1.PackageId == feed2.PackageId &&
                   EqualityComparer<IEnumerable<PackageDependencyGroup>>.Default.Equals(feed1.DependencySetsInternal, feed2.DependencySetsInternal) &&
                   (IgnoreVersion || EqualityComparer<NuGetVersion>.Default.Equals(feed1.Version, feed2.Version)) &&
                   (IgnoreVersion || EqualityComparer<VersionInfo[]>.Default.Equals(feed1.ParsedVersions, feed2.ParsedVersions)) &&

                   feed1.LicenseExpression == feed2.LicenseExpression &&
                   feed1.LicenseExpressionVersion == feed2.LicenseExpressionVersion &&
                   EqualityComparer<LicenseMetadata>.Default.Equals(feed1.LicenseMetadata, feed2.LicenseMetadata) &&

                   EqualityComparer<PackageDeprecationMetadata>.Default.Equals(feed1.DeprecationMetadata, feed2.DeprecationMetadata);
        }

        private bool ClonedPackageSearchMetadataEquals(ClonedPackageSearchMetadata feed1, ClonedPackageSearchMetadata feed2)
        {
            return feed1.Authors == feed2.Authors &&
                       EqualityComparer<IEnumerable<PackageDependencyGroup>>.Default.Equals(feed1.DependencySets, feed2.DependencySets) &&
                       feed1.Description == feed2.Description &&
                       feed1.DownloadCount == feed2.DownloadCount &&
                       EqualityComparer<Uri>.Default.Equals(feed1.IconUrl, feed2.IconUrl) &&
                       (IgnoreVersion ? feed1.Identity.Id.Equals(feed2.Identity.Id) : EqualityComparer<PackageIdentity>.Default.Equals(feed1.Identity, feed2.Identity)) &&
                       EqualityComparer<Uri>.Default.Equals(feed1.LicenseUrl, feed2.LicenseUrl) &&
                       feed1.Owners == feed2.Owners &&
                       EqualityComparer<Uri>.Default.Equals(feed1.ProjectUrl, feed2.ProjectUrl) &&
                       EqualityComparer<DateTimeOffset?>.Default.Equals(feed1.Published, feed2.Published) &&
                       EqualityComparer<Uri>.Default.Equals(feed1.ReportAbuseUrl, feed2.ReportAbuseUrl) &&
                       EqualityComparer<Uri>.Default.Equals(feed1.PackageDetailsUrl, feed2.PackageDetailsUrl) &&
                       feed1.RequireLicenseAcceptance == feed2.RequireLicenseAcceptance &&
                       feed1.Summary == feed2.Summary &&
                       feed1.Tags == feed2.Tags &&
                       feed1.Title == feed2.Title &&
                       feed1.IsListed == feed2.IsListed &&
                       feed1.PrefixReserved == feed2.PrefixReserved &&
                       EqualityComparer<LicenseMetadata>.Default.Equals(feed1.LicenseMetadata, feed2.LicenseMetadata) &&

                       EqualityComparer<Func<PackageReaderBase>>.Default.Equals(feed1.PackageReader, feed2.PackageReader);
        }

        private bool PackageSearchMetadataRegistrationEquals(PackageSearchMetadataRegistration feed1, PackageSearchMetadataRegistration feed2)
        {
            return feed1.Authors == feed2.Authors &&
                   EqualityComparer<IEnumerable<PackageDependencyGroup>>.Default.Equals(feed1.DependencySets, feed2.DependencySets) &&
                   feed1.Description == feed2.Description &&
                   feed1.DownloadCount == feed2.DownloadCount &&
                   EqualityComparer<Uri>.Default.Equals(feed1.IconUrl, feed2.IconUrl) &&
                   (IgnoreVersion ? feed1.Identity.Id.Equals(feed2.Identity.Id) : EqualityComparer<PackageIdentity>.Default.Equals(feed1.Identity, feed2.Identity)) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.LicenseUrl, feed2.LicenseUrl) &&
                   feed1.Owners == feed2.Owners &&
                   EqualityComparer<Uri>.Default.Equals(feed1.ProjectUrl, feed2.ProjectUrl) &&
                   EqualityComparer<DateTimeOffset?>.Default.Equals(feed1.Published, feed2.Published) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.ReportAbuseUrl, feed2.ReportAbuseUrl) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.PackageDetailsUrl, feed2.PackageDetailsUrl) &&
                   feed1.RequireLicenseAcceptance == feed2.RequireLicenseAcceptance &&
                   feed1.Summary == feed2.Summary &&
                   feed1.Tags == feed2.Tags &&
                   feed1.Title == feed2.Title &&
                   feed1.IsListed == feed2.IsListed &&
                   feed1.PrefixReserved == feed2.PrefixReserved &&

                   feed1.PackageId == feed2.PackageId &&
                   EqualityComparer<IEnumerable<PackageDependencyGroup>>.Default.Equals(feed1.DependencySetsInternal, feed2.DependencySetsInternal) &&
                   (IgnoreVersion || EqualityComparer<NuGetVersion>.Default.Equals(feed1.Version, feed2.Version)) &&
                   (IgnoreVersion || EqualityComparer<VersionInfo[]>.Default.Equals(feed1.ParsedVersions, feed2.ParsedVersions)) &&

                   feed1.LicenseExpression == feed2.LicenseExpression &&
                   feed1.LicenseExpressionVersion == feed2.LicenseExpressionVersion &&
                   EqualityComparer<LicenseMetadata>.Default.Equals(feed1.LicenseMetadata, feed2.LicenseMetadata) &&

                   EqualityComparer<PackageDeprecationMetadata>.Default.Equals(feed1.DeprecationMetadata, feed2.DeprecationMetadata) &&
                   EqualityComparer<Uri>.Default.Equals(feed1.CatalogUri, feed2.CatalogUri);
        }

        private bool PackageSearchMetadataV2FeedEquals(PackageSearchMetadataV2Feed feed1, PackageSearchMetadataV2Feed feed2)
        {
            return feed1.Authors == feed2.Authors &&
                  EqualityComparer<IEnumerable<PackageDependencyGroup>>.Default.Equals(feed1.DependencySets, feed2.DependencySets) &&
                  feed1.Description == feed2.Description &&
                  feed1.DownloadCount == feed2.DownloadCount &&
                  EqualityComparer<Uri>.Default.Equals(feed1.IconUrl, feed2.IconUrl) &&
                  (IgnoreVersion ? feed1.Identity.Id.Equals(feed2.Identity.Id) : EqualityComparer<PackageIdentity>.Default.Equals(feed1.Identity, feed2.Identity)) &&
                  EqualityComparer<Uri>.Default.Equals(feed1.LicenseUrl, feed2.LicenseUrl) &&
                  feed1.Owners == feed2.Owners &&

                  EqualityComparer<Uri>.Default.Equals(feed1.ProjectUrl, feed2.ProjectUrl) &&

                  EqualityComparer<DateTimeOffset?>.Default.Equals(feed1.Published, feed2.Published) &&
                  EqualityComparer<Uri>.Default.Equals(feed1.ReportAbuseUrl, feed2.ReportAbuseUrl) &&
                  EqualityComparer<Uri>.Default.Equals(feed1.PackageDetailsUrl, feed2.PackageDetailsUrl) &&
                  feed1.RequireLicenseAcceptance == feed2.RequireLicenseAcceptance &&
                  feed1.Summary == feed2.Summary &&
                  feed1.Tags == feed2.Tags &&
                  feed1.Title == feed2.Title &&
                  feed1.IsListed == feed2.IsListed &&
                  feed1.PrefixReserved == feed2.PrefixReserved &&

                  feed1.PackageId == feed2.PackageId &&
                  EqualityComparer<LicenseMetadata>.Default.Equals(feed1.LicenseMetadata, feed2.LicenseMetadata) &&

                  (IgnoreVersion || EqualityComparer<NuGetVersion>.Default.Equals(feed1.Version, feed2.Version)) &&
                  (IgnoreVersion || EqualityComparer<PackageDeprecationMetadata>.Default.Equals(feed1.DeprecationMetadata, feed2.DeprecationMetadata)) &&

                  EqualityComparer<DateTimeOffset?>.Default.Equals(feed1.Created, feed2.Created) &&
                  EqualityComparer<DateTimeOffset?>.Default.Equals(feed1.LastEdited, feed2.LastEdited);
        }

        public int GetHashCode(IPackageSearchMetadata obj)
        {
            throw new NotImplementedException();
        }
    }
}
