using NuGet.Packaging;
using NuGet.Packaging.Signing;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Linq;
using System.Text;
using System.IO;

namespace ConsoleApp1
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            Console.WriteLine("pls enter the pacakge path...");
            string packagePath = Console.ReadLine();
            while (!File.Exists(packagePath))
            {
                Console.WriteLine($"{packagePath} doesn't exist, pls enter another one");
                packagePath = Console.ReadLine();
            }
            //string packagePath = "C:\\Users\\henli\\Downloads\\newtonsoft.json.12.0.3.nupkg";
            //string packagePath = "C:\\Users\\henli\\Downloads\\rido.coreapplauncher.0.0.1-beta1.nupkg";

            using (var packageReader = new PackageArchiveReader(packagePath))
            {
                CancellationToken cancellationToken = CancellationToken.None;
                var primarySignature = await packageReader.GetPrimarySignatureAsync(cancellationToken);

                DateTime upperdate = new DateTime();
                foreach (var timestamp in primarySignature.Timestamps)
                {
                    upperdate = timestamp.UpperLimit.LocalDateTime;
                    Console.WriteLine($"Timestamp.UpperLimit.LocalDateTime on this signature is {upperdate}");
                }

                var certificateExtraStore = primarySignature.SignedCms.Certificates;

                X509Certificate2 certToBuildChain = null;
                bool found = false;
                foreach (var cert in certificateExtraStore)
                {
                    if (cert.Subject.Contains("Ricardo Minguez"))
                    {
                        found = true;
                        certToBuildChain = cert;
                    }
                }
                if (!found)
                {
                    Console.WriteLine("Cert not found!");
                    return;
                }

                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;

                    // This flag should only be set for verification scenarios, not signing.
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                    chain.ChainPolicy.ExtraStore.AddRange(certificateExtraStore);

                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                    string overallStatus;

                    if (chain.Build(certToBuildChain))
                    {
                        overallStatus = "OK";
                    }
                    else
                    {
                        IOrderedEnumerable<string> statuses = GetOverallChainStatus(chain);

                        overallStatus = $"\"{string.Join(",", statuses)}\"";
                    }
                    if (chain.ChainElements.Count == 3)
                    {
                        string chainFingerprints = GetChainFingerprints(chain.ChainElements);
                        Console.WriteLine($"==========  Without setting X509ChainPolicy.VerificationTime ==============");
                        Console.WriteLine($"   chain building for {certToBuildChain.Subject}    ");
                        Console.WriteLine($"{chainFingerprints} \n Status : {overallStatus} \n");
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        var file = new FileInfo(Path.Combine(".", $"{i}.cer"));
                        File.WriteAllBytes(file.FullName, chain.ChainElements[i].Certificate.RawData);
                        Console.WriteLine($"{chain.ChainElements[i].Certificate.Subject} is written to {file.FullName}");
                    }
                }

                using (var chainHolder = new X509ChainHolder())
                {
                    var chain = chainHolder.Chain;

                    // This flag should only be set for verification scenarios, not signing.
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                    chain.ChainPolicy.ExtraStore.AddRange(certificateExtraStore);

                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

                    chain.ChainPolicy.VerificationTime = upperdate;

                    string overallStatus;

                    if (chain.Build(certToBuildChain))
                    {
                        overallStatus = "OK";
                    }
                    else
                    {
                        IOrderedEnumerable<string> statuses = GetOverallChainStatus(chain);

                        overallStatus = $"\"{string.Join(",", statuses)}\"";
                    }
                    if (chain.ChainElements.Count == 3)
                    {
                        string chainFingerprints = GetChainFingerprints(chain.ChainElements);
                        Console.WriteLine($"==========  Setting X509ChainPolicy.VerificationTime to {upperdate} ==============");
                        Console.WriteLine($"   chain building for {certToBuildChain.Subject}    ");
                        Console.WriteLine($"{chainFingerprints} \n Status : {overallStatus} \n");
                    }

                }
            }
        }
        private static IOrderedEnumerable<string> GetOverallChainStatus(X509Chain chain)
        {
            return chain.ChainStatus
                .Select(chainStatus => chainStatus.Status.ToString())
                .OrderBy(chainStatus => chainStatus);
        }
        private static string GetChainFingerprints(X509ChainElementCollection chainElements)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("\"");

            for (var i = 0; i < chainElements.Count; ++i)
            {
                X509ChainElement chainElement = chainElements[i];

                stringBuilder.Append($"{chainElement.Certificate.Subject},\n");
            }

            stringBuilder[stringBuilder.Length - 1] = '\"';

            return stringBuilder.ToString();
        }
    }
}
