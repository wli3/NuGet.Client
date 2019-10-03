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
namespace NuGet.Packaging.CrossVerify.Generate.Test
{
    [Collection(SigningTestCollection.Name)]
    public class GenerateSignedPackages
    {
        private const string _untrustedChainCertError = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
        private const string _preGeneratePackageFolderName = "PreGenPackages";
        private readonly SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly IList<ISignatureVerificationProvider> _trustProviders;
        private readonly X509Certificate2 _trustedRootCertForTSA;
        private readonly TrustedTestCert<TestCertificate> _trustedRepoTestCert;
        private string _dir;

        public GenerateSignedPackages()
        {
            _testFixture = new SigningTestFixture();
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _trustedRepoTestCert = SigningTestUtility.GenerateTrustedTestCertificate();
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider()
            };
            _dir = CreatePreGenPackageForEachPlatform();

            //generate TSA root cert file under each platform 
            _trustedRootCertForTSA = _testFixture.TrustedServerRootCertificate;
            var tsaRootCertPath = new FileInfo(Path.Combine(_dir, "tsaRoot.cer"));
            var bytes = _trustedRootCertForTSA.RawData;
            File.WriteAllBytes(tsaRootCertPath.FullName, bytes);

        }
        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned()
        {
            // Arrange
            var caseName = "AuthorSigned";

            var nupkg = new SimpleTestPackageContext(caseName);

            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath);

                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, bytes);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 1);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_TimeStamped()
        {
            // Arrange
            var caseName = "AuthorSigned_TimeStamped";

            var nupkg = new SimpleTestPackageContext(caseName);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    timestampService.Url);

                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, bytes);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 1);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_RepositorySigned()
        {
            // Arrange
            var caseName = "RepositorySigned";

            var nupkg = new SimpleTestPackageContext(caseName);

            using (var primaryCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"));

                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, bytes);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 1);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_RepositorySigned_TimeStamped()
        {
            // Arrange
            var caseName = "RepositorySigned_TimeStamped";

            var nupkg = new SimpleTestPackageContext(caseName);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");
                Directory.CreateDirectory(packagePath);

                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    testCertificate,
                    nupkg,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"),
                    timestampService.Url);

                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes = testCertificate.RawData;
                File.WriteAllBytes(CertFile.FullName, bytes);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 1);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_RepositoryCounterSigned()
        {
            // Arrange
            var caseName = "AuthorSigned_RepositoryCounterSigned";

            var nupkg = new SimpleTestPackageContext(caseName);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_testFixture.TrustedTestCertificate.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate, signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"));


                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "R.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 2);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }
   

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositoryCounterSigned()
        {
            // Arrange
            var caseName = "AuthorSigned_Timestamped_RepositoryCounterSigned";

            var nupkg = new SimpleTestPackageContext(caseName);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_testFixture.TrustedTestCertificate.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    timestampService.Url);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate, signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"));


                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "R.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 2);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_RepositoryCounterSigned_Timestamped()
        {
            // Arrange
            var caseName = "AuthorSigned_RepositoryCounterSigned_Timestamped";

            var nupkg = new SimpleTestPackageContext(caseName);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_testFixture.TrustedTestCertificate.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate, signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"),
                    timestampService.Url);


                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "R.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 2);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }

        [Fact]
        public async Task PreGenerateSignedPackages_AuthorSigned_Timestamped_RepositoryCounterSigned_Timestamped()
        {
            // Arrange
            var caseName = "AuthorSigned_Timestamped_RepositoryCounterSigned_Timestamped";

            var nupkg = new SimpleTestPackageContext(caseName);

            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();

            using (var primaryCertificate = new X509Certificate2(_testFixture.TrustedTestCertificate.Source.Cert))
            using (var counterCertificate = new X509Certificate2(_trustedRepoTestCert.Source.Cert))
            {
                string packagePath = Path.Combine(_dir, caseName, "package");

                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    primaryCertificate,
                    nupkg,
                    packagePath,
                    timestampService.Url);

                var countersignedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    counterCertificate, signedPackagePath,
                    packagePath,
                    new Uri("https://v3serviceIndex.test/api/index.json"),
                    timestampService.Url);


                string certFolder = System.IO.Path.Combine(_dir, caseName, "cert");
                Directory.CreateDirectory(certFolder);

                var CertFile1 = new FileInfo(Path.Combine(certFolder, "A.cer"));
                var bytes1 = primaryCertificate.RawData;
                File.WriteAllBytes(CertFile1.FullName, bytes1);

                var CertFile2 = new FileInfo(Path.Combine(certFolder, "R.cer"));
                var bytes2 = counterCertificate.RawData;
                File.WriteAllBytes(CertFile2.FullName, bytes2);

                Assert.Equal(Directory.GetFiles(certFolder).Length, 2);
                Assert.Equal(Directory.GetFiles(packagePath).Length, 1);
            }
        }

        private static string CreatePreGenPackageForEachPlatform()
        {
            var root = TestFileSystemUtility.NuGetTestFolder;
            var path = Path.Combine(root, _preGeneratePackageFolderName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //Create a folder for a each platform, under PreGenPackages folder.
            //For functional test on windows, 2 folders will be created.
            var platform = "";
#if IS_DESKTOP
            platform = "Windows_NetFulFramework";
#else
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                platform =  "Windows_NetCore";
            }
            else if (RuntimeEnvironmentHelper.IsMacOSX)
            {
                platform = "Mac_NetCore";
            }
            else
            {
                platform = "Linux_NetCore";
            }
#endif
            var pathForEachPlatform = Path.Combine(path, platform);

            if (Directory.Exists(pathForEachPlatform))
            {
                Directory.Delete(pathForEachPlatform, recursive: true);
            }
            Directory.CreateDirectory(pathForEachPlatform);

            return pathForEachPlatform;
        }
    }
}
#endif


