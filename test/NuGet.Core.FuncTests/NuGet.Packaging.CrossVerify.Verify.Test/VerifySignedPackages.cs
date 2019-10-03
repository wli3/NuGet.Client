#if IS_SIGNING_SUPPORTED
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using NuGet.Packaging.FuncTest;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Test.Utility;
using Test.Utility.Signing;
using Xunit;
using BcAccuracy = Org.BouncyCastle.Asn1.Tsp.Accuracy;
using DotNetUtilities = Org.BouncyCastle.Security.DotNetUtilities;

namespace NuGet.Packaging.CrossVerify.Verify.Test
{
    [Collection(SigningTestCollection.Name)]
    public class VerifySignedPackages
    {
        private readonly IList<ISignatureVerificationProvider> _trustProviders;
        private string _dir;

        public VerifySignedPackages()
        {
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider()
            };
            _dir = GetPreGenPackageRootPath();
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned(string dir)
        {
            // Arrange
            var caseName = "A";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var certFile = Directory.GetFiles(certFolder).First();

            var tsaRootCertFile = Directory.GetFiles(dir, "tsaRoot.cer", SearchOption.TopDirectoryOnly).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(certFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_TimeStamped(string dir)
        {
            // Arrange
            var caseName = "AT";
            
            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var certFile = Directory.GetFiles(certFolder).First();

            var tsaRootCertFile = Directory.GetFiles(dir, "tsaRoot.cer", SearchOption.TopDirectoryOnly).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(certFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(tsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_RepositorySigned(string dir)
        {
            // Arrange
            var caseName = "R";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var certFile = Directory.GetFiles(certFolder).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(certFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_RepositorySigned_Timestamped(string dir)
        {
            // Arrange
            var caseName = "RT";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var certFile = Directory.GetFiles(certFolder).First();

            var tsaRootCertFile = Directory.GetFiles(dir, "tsaRoot.cer", SearchOption.TopDirectoryOnly).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(certFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(tsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_RepositorySigned(string dir)
        {
            // Arrange
            var caseName = "AR";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var primaryCertFile = Directory.GetFiles(certFolder).First();
            var counterCertFile = Directory.GetFiles(certFolder).Last();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(primaryCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(counterCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositorySigned(string dir)
        {
            // Arrange
            var caseName = "ATR";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var primaryCertFile = Directory.GetFiles(certFolder).First();
            var counterCertFile = Directory.GetFiles(certFolder).Last();

            var tsaRootCertFile = Directory.GetFiles(dir, "tsaRoot.cer", SearchOption.TopDirectoryOnly).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(primaryCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(counterCertFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(tsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_RepositorySigned_Timestamped(string dir)
        {
            // Arrange
            var caseName = "ART";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var primaryCertFile = Directory.GetFiles(certFolder).First();
            var counterCertFile = Directory.GetFiles(certFolder).Last();

            var tsaRootCertFile = Directory.GetFiles(dir, "tsaRoot.cer", SearchOption.TopDirectoryOnly).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(primaryCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(counterCertFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(tsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }

        [Theory]
        [MemberData(nameof(FolderForEachPlatform))]
        public async Task VerifySignaturesAsync_PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositorySigned_Timestamped(string dir)
        {
            // Arrange
            var caseName = "ATRT";

            var settings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            var signedPackageFolder = Path.Combine(dir, caseName, "package");
            var signedPackagePath = Directory.GetFiles(signedPackageFolder).First();

            var certFolder = Path.Combine(dir, caseName, "cert");
            var primaryCertFile = Directory.GetFiles(certFolder).First();
            var counterCertFile = Directory.GetFiles(certFolder).Last();

            var tsaRootCertFile = Directory.GetFiles(dir, "tsaRoot.cer", SearchOption.TopDirectoryOnly).First();

            using (var primaryCertificate = new X509Certificate2(File.ReadAllBytes(primaryCertFile)))
            using (var counterCertificate = new X509Certificate2(File.ReadAllBytes(counterCertFile)))
            using (var tsaRootCertificate = new X509Certificate2(File.ReadAllBytes(tsaRootCertFile)))
            using (var packageReader = new PackageArchiveReader(signedPackagePath))
            using (var store = new X509Store(StoreName.Root,
                RuntimeEnvironmentHelper.IsWindows ? StoreLocation.LocalMachine : StoreLocation.CurrentUser))
            {
                AddCertificateToStore(primaryCertificate, store);
                AddCertificateToStore(counterCertificate, store);
                AddCertificateToStore(tsaRootCertificate, store);

                var verifier = new PackageSignatureVerifier(_trustProviders);
                // Act
                var result = await verifier.VerifySignaturesAsync(packageReader, settings, CancellationToken.None);
                var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());
                var resultsWithWarnings = result.Results.Where(r => r.GetWarningIssues().Any());

                // Assert
                try
                {
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                    resultsWithWarnings.Count().Should().Be(0);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + GetResultIssues(result, dir));
                }
            }
        }
        private static string GetPreGenPackageRootPath()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = System.IO.Path.Combine(root, "PreGenPackages");
            return path;
        }

        /*
        private void CreateCertificateStore()
        {
            StoreName storeName = StoreName.Root;
            StoreLocation storeLocation = StoreLocation.LocalMachine;

            if (RuntimeEnvironmentHelper.IsLinux)
            {
                storeName = StoreName.Root;
                storeLocation = StoreLocation.CurrentUser;
            }

            store = new X509Store(storeName, storeLocation);
        }
        */

        private void AddCertificateToStore(X509Certificate2 cert, X509Store store)
        {
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);

        }

        private string GetResultIssues(VerifySignaturesResult result, string dir)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"verify package from : {dir}");
            int i = 0;
            foreach (var rst in result.Results)
            {
                sb.AppendLine($"result {i}:  {rst.Trust.ToString()}");
                foreach (var error in rst.Issues.Where(issue => issue.Level == LogLevel.Error))
                {
                    sb.AppendLine($"   error :  {error.Code} : {error.Message}");
                }
                foreach (var warning in rst.Issues.Where(issue => issue.Level == LogLevel.Warning))
                {
                    sb.AppendLine($"   warning :  {warning.Code} : {warning.Message}");
                }
                i++;
            }
            return sb.ToString();
        }

        public static TheoryData FolderForEachPlatform
        {
            get
            {
                /* should have 4 folders:
                    "Windows_NetFulFramework",
                    "Windows_NetCore",
                    "Mac_NetCore",
                    "Linux_NetCore",
                */
                var folders = new TheoryData<string>();
                foreach (var folder in Directory.GetDirectories(GetPreGenPackageRootPath()))
                {
                    folders.Add(folder);
                }

                return folders;
            }
        }
    }
}
#endif

