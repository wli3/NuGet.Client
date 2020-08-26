using System;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using System.Collections.Generic;
using System.Threading;
using System.Text;

namespace ConsoleApp1
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            string packagePath = "/home/henli/demo/packages/rido.coreapplauncher.0.0.1-beta1.nupkg";
            //string packagePath = "C:\\Users\\henli\\Downloads\\rido.coreapplauncher.0.0.1-beta1.nupkg";
            using (var packageReader = new PackageArchiveReader(packagePath))
            {
                var verifierSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();
                var verificationProviders = new List<ISignatureVerificationProvider>()
                {
                    new IntegrityVerificationProvider(),
                    new SignatureTrustAndValidityVerificationProvider()
                };
                var verifier = new PackageSignatureVerifier(verificationProviders);
                var verificationResult = await verifier.VerifySignaturesAsync(packageReader, verifierSettings, CancellationToken.None);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Is valid : " + verificationResult.IsValid.ToString());
                int i = 0, w = 0, e = 0;
                foreach (var result in verificationResult.Results)
                {
                    w = 0;
                    e = 0;
                    sb.AppendLine($"Result {i++} : ");
                    foreach (var warning in result.GetWarningIssues())
                    {
                        sb.AppendLine($"   Warning {w++} : ");
                    }
                    foreach (var error in result.GetErrorIssues())
                    {
                        sb.AppendLine($"   Error {e++} : ");
                    }
                }
                Console.WriteLine(sb.ToString());
            }
        }
    }
}
