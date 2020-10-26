// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_SIGNING_SUPPORTED

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Test.Utility;
using Test.Utility.Signing;
using Xunit;
using BcAccuracy = Org.BouncyCastle.Asn1.Tsp.Accuracy;
using DotNetUtilities = Org.BouncyCastle.Security.DotNetUtilities;
using HashAlgorithmName = NuGet.Common.HashAlgorithmName;
#if NETCOREAPP5_0
using Dotnet.Integration.Test;
#else
using NuGet.CommandLine.Test;
#endif

namespace NuGet.Signing.CrossFramework.Test
{
    [Collection(SigningTestCollection.Name)]
    public class CrossFrameworkVerificationTest
    {
        private readonly SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly TestCertificate _untrustedTestCertificate;
        private readonly IList<ISignatureVerificationProvider> _trustProviders;
#if NETCOREAPP5_0
        private readonly string _dotnetCommand;
#else
        private readonly string _nugetCommand;
#endif

        public CrossFrameworkVerificationTest(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };
#if NETCOREAPP5_0
            msbuildFixture = new MsbuildIntegrationTestFixture();
            _dotnetCommand = msbuildFixture.
#else
            
            _nugetExePath = Util.GetNuGetExePath();
#endif
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ValidCertificate_SuccessAsync()
        {
            // Arrange
            var nupkg = new SimpleTestPackageContext();

            using (var dir = TestDirectory.Create())
            using (var testCertificate = new X509Certificate2(_trustedTestCert.Source.Cert))
            {
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(testCertificate, nupkg, dir);
                var verifier = new PackageSignatureVerifier(_trustProviders);
                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    // Act
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var resultsWithErrors = result.Results.Where(r => r.GetErrorIssues().Any());

                    // Assert
                    result.IsValid.Should().BeTrue();
                    resultsWithErrors.Count().Should().Be(0);
                }
            }
        }
