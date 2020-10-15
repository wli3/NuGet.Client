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
using System.Security.Cryptography;
using System.IO;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========Console2 test start=======");
            var _testFixture = new SigningTestFixture();
            var _trustedTestCert = _testFixture.TrustedTestCertificate;
            var _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
            var _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };

            Console.WriteLine("Pls input a number :");
            int number = Int32.Parse(Console.ReadLine());
            Console.WriteLine($"{number} is entered , so test will run {number} times");

            for (int i = 0; i < number; i++)
            {
                Console.WriteLine("\n test : " + i + "\n");
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

                    var waitDuration = (notAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(2));

                    // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                    await Task.Delay(waitDuration);

                    Assert.True(DateTime.UtcNow > notAfter);

                    var verifier = new PackageSignatureVerifier(_trustProviders);

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

                        if (trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0)
                        {
                            Console.WriteLine("test finished as expected");
                        }
                        else
                        {
                            Console.WriteLine(msg + "\n");
                            //another way to build chain:
                            var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                            var timestamps = primarySignature.Timestamps;
                            Console.WriteLine("timestamps.Count is : " + timestamps.Count);

                            var timestamp = timestamps.FirstOrDefault();
                            if (timestamp != null)
                            {
                                var certificateExtraStore = timestamp.SignedCms.Certificates;

                                using (var chainHolder = new X509ChainHolder())
                                {
                                    var chain = chainHolder.Chain;

                                    // This flag should only be set for verification scenarios, not signing.
                                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
                                    chain.ChainPolicy.ApplicationPolicy.Add(new Oid(Oids.TimeStampingEku));
                                    chain.ChainPolicy.ExtraStore.AddRange(certificateExtraStore);
                                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;


                                    var timestamperCertificate = timestamp.SignerInfo.Certificate;
                                    var buildSuccess = chain.Build(timestamperCertificate);
                                    var status = new X509ChainStatus[chain.ChainStatus.Length];
                                    chain.ChainStatus.CopyTo(status, 0);
                                    Console.WriteLine("another way to build chain, result is : " + buildSuccess + "\n");

                                    bool buildSuccessAndNotInFuture = buildSuccess && !CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate);
                                    Console.WriteLine("another way to build chain, buildSuccessAndNotInFuture is : " + buildSuccessAndNotInFuture + "\n");

                                    //display detailed chain build info
                                    var elements = new StringBuilder();
                                    elements.AppendLine("certificate.Subject is : " + certificate.Subject);
                                    elements.AppendLine("certificate.FriendlyName is : " + certificate.FriendlyName);
                                    int k = 0;
                                    foreach (var chainElement in chain.ChainElements)
                                    {
                                        elements.AppendLine($"=================   The ({k}) th certificate is   :=================");
                                        elements.AppendLine($"chainElement.Certificate.Subject : ({chainElement.Certificate.Subject})");
                                        elements.AppendLine($"chainElement.Certificate.Thumbprint : ({chainElement.Certificate.Thumbprint})");
                                        elements.AppendLine($"chainElement.Certificate.isvalid : ({chainElement.Certificate.Verify()})");

                                        //display more details
                                        elements.AppendLine($"    --------   The chainElementStatus are : -------");
                                        foreach (var chainElementStatus in chainElement.ChainElementStatus)
                                        {
                                            elements.AppendLine($"  status : ({chainElementStatus.Status.ToString()})");
                                            elements.AppendLine($"  info: ({chainElementStatus.StatusInformation})");
                                        }

                                        //write certs to disk
                                        var file = new FileInfo(Path.Combine(".", $"{k}.cer"));
                                        File.WriteAllBytes(file.FullName, chainElement.Certificate.RawData);
                                        Console.WriteLine($"cert {k} is written to {file.FullName}");
                                        k++;
                                    }
                                }


                                //verify for the second time
                                var result2 = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                                var trustProvider2 = result2.Results.Single();

                                StringBuilder warnings2 = new StringBuilder("warnings :");
                                foreach (var warning in trustProvider2.GetWarningIssues())
                                {
                                    warnings2.AppendLine(warning.Message);
                                }

                                StringBuilder errors2 = new StringBuilder("errors :");
                                foreach (var error in trustProvider2.GetErrorIssues())
                                {
                                    errors2.AppendLine(error.Message);
                                }

                                StringBuilder results2 = new StringBuilder("all results :");
                                foreach (var r in trustProvider2.Issues)
                                {
                                    results2.AppendLine(r.Message);
                                }
                                var msg2 = warnings2.ToString() + "\n" + errors2.ToString() + "\n" + results2.ToString();
                                Console.WriteLine("Verify for the second time: \n" + msg2);

                                Console.WriteLine("Press [ENTER] to continue");
                                Console.ReadLine();
                            }
                        }

                    }
                }
            }
        }
    }
}
