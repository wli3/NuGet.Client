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

namespace ConsoleApp2
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========test start=======");

            Console.WriteLine("Pls enter a number, to specify how many times do you want the test to run :");
            int testRunCount = Int32.Parse(Console.ReadLine());
            Console.WriteLine($"{testRunCount} is entered , so test will run {testRunCount} times");

            Console.WriteLine("Pls enter a number, to specify how many seconds do you want to wait after the certificate get expired (1 is the minimum) :");
            int delaySec = Int32.Parse(Console.ReadLine());
            Console.WriteLine($"{delaySec} is entered , so the chain build will run after {delaySec} sec after the certificate get expired");

            var _testFixture = new SigningTestFixture();
            var _trustedTestCert = _testFixture.TrustedTestCertificate;
            var _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
            var _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };

            int sucessChainBuild1 = 0;
            int sucessChainBuild2 = 0;

            for (int i = 0; i < testRunCount; i++)
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

                    var waitDuration = (notAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(delaySec));

                    // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                    await Task.Delay(waitDuration);

                    Assert.True(DateTime.UtcNow > notAfter);

                    using (var packageReader = new PackageArchiveReader(signedPackagePath))
                    {
                        var primarySignature = await packageReader.GetPrimarySignatureAsync(CancellationToken.None);
                        var timestamps = primarySignature.Timestamps;

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

                                for (int j = 1; j <= 2; j++)
                                {
                                    var buildSuccess = chain.Build(timestamperCertificate);
                                    if (buildSuccess)
                                    {
                                        if (j == 1)
                                        {
                                            sucessChainBuild1++;
                                        }
                                        else
                                        {
                                            sucessChainBuild2++;
                                        }
                                    }
                                    var status = new X509ChainStatus[chain.ChainStatus.Length];
                                    chain.ChainStatus.CopyTo(status, 0);
                                    bool buildSuccessAndNotInFuture = buildSuccess && !CertificateUtility.IsCertificateValidityPeriodInTheFuture(certificate);
                                    if (buildSuccessAndNotInFuture)
                                    {
                                        Console.WriteLine($"Try {j} : pass");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Try {j} : failed");
                                    }
                                
                                    //If chain build is not successful, display detailed chain build info
                                    if (!buildSuccess)
                                    {
                                        var elements = new StringBuilder();
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

                                            //var file = new FileInfo(Path.Combine(".", $"{k}.cer"));
                                            //File.WriteAllBytes(file.FullName, chainElement.Certificate.RawData);
                                            //Console.WriteLine($"cert {k} is written to {file.FullName}");
                                            k++;
                                        }
                                        Console.WriteLine("Press [ENTER] to continue");
                                        Console.ReadLine();
                                    }
                                }
                                
                            }
                        }
                    }

                }
            }
            Console.WriteLine("===============summary=====================");
            Console.WriteLine($"If delay {delaySec} sec after the certificate get expired: ");
            Console.WriteLine($"The 1st chain build pass {sucessChainBuild1}/{testRunCount}");
            Console.WriteLine($"The 2nd chain build pass {sucessChainBuild2}/{testRunCount}");
        }
    }
}
