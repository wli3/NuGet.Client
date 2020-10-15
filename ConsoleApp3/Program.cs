using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Test.Utility;
using Test.Utility.Signing;
using NuGet.Packaging.FuncTest;
using Org.BouncyCastle.Asn1.X509;
using NuGet.Packaging;
using Xunit;
using System.Text;

namespace ConsoleApp2
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========Console3 test start=======");
            var _testFixture = new SigningTestFixture();
            var _trustedTestCert = _testFixture.TrustedTestCertificate;
            var _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
            var _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };

            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var timestampService = await _testFixture.GetDefaultTrustedTimestampServiceAsync();
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);
            var now = DateTimeOffset.UtcNow;
            var issueOptions = new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                NotAfter = now.AddSeconds(10),
                NotBefore = now.AddSeconds(-2),
                SubjectName = new X509Name("CN=NuGet Test Expired Certificate")
            };
            var bcCertificate = ca.IssueCertificate(issueOptions);

            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var notAfter = certificate.NotAfter.ToUniversalTime();

                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (notAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                await Task.Delay(waitDuration);

                if (DateTime.UtcNow > notAfter)
                {
                    Console.WriteLine("As expected: DateTime.UtcNow > notAfter");
                }
                else
                {
                    Console.WriteLine("Not as expected: DateTime.UtcNow <= notAfter");
                    return;
                }

                var verifier = new PackageSignatureVerifier(_trustProviders);

                for (int i = 0; i < 100; i++)
                {
                    Console.WriteLine("\n test : " + i + "\n");

                    using (var packageReader = new PackageArchiveReader(signedPackagePath))
                    {
                        var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                        var trustProvider = result.Results.Single();

                        StringBuilder warnings = new StringBuilder("warnings :");
                        foreach (var warning in trustProvider.GetWarningIssues())
                        {
                            warnings.AppendLine(warning.Message);
                        }

                        StringBuilder errors = new StringBuilder("errors :");
                        foreach (var error in trustProvider.GetErrorIssues())
                        {
                            errors.AppendLine(error.Message);
                        }

                        StringBuilder results = new StringBuilder("all results :");
                        foreach (var r in trustProvider.Issues)
                        {
                            results.AppendLine(r.Message);
                        }
                        var msg = warnings.ToString() + "\n" + errors.ToString() + "\n" + results.ToString();

                        Assert.True(result.IsValid);
                        Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                        Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                        Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                    }
                }
            }
        }
    }
}

