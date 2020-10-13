// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if IS_SIGNING_SUPPORTED

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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

namespace NuGet.Packaging.FuncTest
{
    [Collection(SigningTestCollection.Name)]
    public class SignatureTrustAndValidityVerificationProviderTests
    {
        private const string _untrustedChainCertError = "A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.";
        private readonly SignedPackageVerifierSettings _verifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault(TestEnvironmentVariableReader.EmptyInstance);
        private readonly SigningTestFixture _testFixture;
        private readonly TrustedTestCert<TestCertificate> _trustedTestCert;
        private readonly TestCertificate _untrustedTestCertificate;
        private readonly IList<ISignatureVerificationProvider> _trustProviders;

        public SignatureTrustAndValidityVerificationProviderTests(SigningTestFixture fixture)
        {
            _testFixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _trustedTestCert = _testFixture.TrustedTestCertificate;
            _untrustedTestCertificate = _testFixture.UntrustedTestCertificate;
            _trustProviders = new List<ISignatureVerificationProvider>()
            {
                new SignatureTrustAndValidityVerificationProvider()
            };
        }


        //[PlatformFact(Platform.Windows, Platform.Darwin)] // https://github.com/NuGet/Home/issues/9771
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_1()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_2()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_3()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_4()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_5()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_6()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_7()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_8()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_9()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_10()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_11()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_12()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_13()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_14()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_15()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_16()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_17()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_18()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_19()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_20()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_21()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_22()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_23()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_24()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_25()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_26()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_27()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_28()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_29()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestamp_SuccessAsync_30()
        {
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

                Assert.True(DateTime.UtcNow > notAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var result = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);

                    var trustProvider = result.Results.Single();

                    var msg = GetErrorsAndWarnings(trustProvider);

                    Assert.True(result.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Valid, trustProvider.Trust);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Error) == 0, msg);
                    Assert.True(trustProvider.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);
                }
            }
        }

        //[PlatformFact(Platform.Windows)] // https://github.com/NuGet/Home/issues/9763
        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_1()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_2()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_3()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_4()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                   
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_5()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_6()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_7()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                   
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_8()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_9()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                   
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_10()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                   
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_11()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_12()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_13()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                   
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_14()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);
                    
                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_15()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_16()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_17()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_18()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_19()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_20()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_21()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_22()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_23()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_24()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_25()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_26()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_27()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_28()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_29()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        [CIOnlyFact]
        public async Task VerifySignaturesAsync_ExpiredCertificateAndTimestampWithTooLargeRange_FailsAsync_30()
        {
            var testServer = await _testFixture.GetSigningTestServerAsync();
            var ca = await _testFixture.GetDefaultTrustedCertificateAuthorityAsync();
            var accuracy = new BcAccuracy(seconds: new DerInteger(30), millis: null, micros: null);
            var serviceOptions = new TimestampServiceOptions() { Accuracy = accuracy };
            var timestampService = TimestampService.Create(ca, serviceOptions);
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

            using (testServer.RegisterResponder(timestampService))
            using (var directory = TestDirectory.Create())
            using (X509Certificate2 certificate = CertificateUtilities.GetCertificateWithPrivateKey(bcCertificate, keyPair))
            {
                var packageContext = new SimpleTestPackageContext();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampService.Url);

                var waitDuration = (issueOptions.NotAfter - DateTimeOffset.UtcNow).Add(TimeSpan.FromSeconds(1));

                // Wait for the certificate to expire.  Trust of the signature will require a valid timestamp.
                if (waitDuration > TimeSpan.Zero)
                {
                    await Task.Delay(waitDuration);
                }

                Assert.True(DateTime.UtcNow > issueOptions.NotAfter);

                var verifier = new PackageSignatureVerifier(_trustProviders);

                using (var packageReader = new PackageArchiveReader(signedPackagePath))
                {
                    var results = await verifier.VerifySignaturesAsync(packageReader, _verifyCommandSettings, CancellationToken.None);
                    var result = results.Results.Single();
                    var msg = GetErrorsAndWarnings(result);

                    Assert.False(results.IsValid);
                    Assert.Equal(SignatureVerificationStatus.Disallowed, result.Trust);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Error) == 1, msg);
                    Assert.True(result.Issues.Count(issue => issue.Level == LogLevel.Warning) == 0, msg);

                    Assert.Contains(result.Issues, issue =>
                        issue.Code == NuGetLogCode.NU3037 &&
                        issue.Level == LogLevel.Error &&
                        issue.Message.Contains("validity period has expired."));
                }
            }
        }

        private sealed class Test : IDisposable
        {
            private readonly TestDirectory _directory;
            private bool _isDisposed;

            internal FileInfo PackageFile { get; }

            private Test(TestDirectory directory, FileInfo package)
            {
                _directory = directory;
                PackageFile = package;
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _directory.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }

            internal static async Task<Test> CreateAuthorSignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.AuthorSignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    timestampServiceUrl);

                return new Test(directory, new FileInfo(signedPackagePath));
            }

            internal static async Task<Test> CreateRepositoryPrimarySignedPackageAsync(
                X509Certificate2 certificate,
                Uri timestampServiceUrl = null)
            {
                var packageContext = new SimpleTestPackageContext();
                var directory = TestDirectory.Create();
                var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                    certificate,
                    packageContext,
                    directory,
                    new Uri("https://nuget.test"),
                    timestampServiceUrl);

                return new Test(directory, new FileInfo(signedPackagePath));
            }

            internal static async Task<Test> CreateAuthorSignedRepositoryCountersignedPackageAsync(
                X509Certificate2 authorCertificate,
                X509Certificate2 repositoryCertificate,
                Uri authorTimestampServiceUrl = null,
                Uri repoTimestampServiceUrl = null)
            {
                var directory = TestDirectory.Create();

                using (var test = await CreateAuthorSignedPackageAsync(authorCertificate, authorTimestampServiceUrl))
                {
                    var signedPackagePath = await SignedArchiveTestUtility.RepositorySignPackageAsync(
                        repositoryCertificate,
                        test.PackageFile.FullName,
                        directory,
                        new Uri("https://nuget.test"),
                        repoTimestampServiceUrl);

                    return new Test(directory, new FileInfo(signedPackagePath));
                }
            }
        }

        private static async Task<List<SignatureLog>> VerifyUnavailableRevocationInfoAsync(
            SignatureVerificationStatus expectedStatus,
            LogLevel expectedLogLevel,
            SignedPackageVerifierSettings settings,
            string resourceName = "UnavailableCrlPackage.nupkg")
        {
            var verificationProvider = new SignatureTrustAndValidityVerificationProvider();

            using (var nupkgStream = new MemoryStream(GetResource(resourceName)))
            using (var package = new PackageArchiveReader(nupkgStream, leaveStreamOpen: false))
            {
                // Read a signature that is valid in every way except that the CRL information is unavailable.
                var signature = await package.GetPrimarySignatureAsync(CancellationToken.None);

                using (TrustPrimaryRootCertificate(signature))
                using (TrustPrimaryTimestampRootCertificate(signature))
                {
                    // Act
                    var result = await verificationProvider.GetTrustResultAsync(package, signature, settings, CancellationToken.None);

                    // Assert
                    Assert.Equal(expectedStatus, result.Trust);
                    return result
                        .Issues
                        .Where(x => x.Level >= expectedLogLevel)
                        .OrderBy(x => x.Message)
                        .ToList();
                }
            }
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.Packaging.FuncTest.compiler.resources.{name}",
                typeof(SignatureTrustAndValidityVerificationProviderTests));
        }

        private static IDisposable TrustPrimaryRootCertificate(PrimarySignature signature)
        {
            using (var certificateChain = SignatureUtility.GetCertificateChain(signature))
            {
                return TrustRootCertificate(certificateChain);
            }
        }

        private static IDisposable TrustPrimaryTimestampRootCertificate(PrimarySignature signature)
        {
            var timestamp = signature.Timestamps.FirstOrDefault();

            if (timestamp == null)
            {
                return null;
            }

            using (var certificateChain = SignatureUtility.GetTimestampCertificateChain(signature))
            {
                return TrustRootCertificate(certificateChain);
            }
        }

        private static IDisposable TrustRootCertificate(IX509CertificateChain certificateChain)
        {
            var rootCertificate = certificateChain.Last();
            StoreLocation storeLocation = CertificateStoreUtilities.GetTrustedCertificateStoreLocation();

            return TrustedTestCert.Create(
                new X509Certificate2(rootCertificate),
                StoreName.Root,
                storeLocation,
                maximumValidityPeriod: TimeSpan.MaxValue);
        }

        private string GetErrorsAndWarnings(PackageVerificationResult result)
        {
            StringBuilder warnings = new StringBuilder("warnings :");
            foreach (var warning in result.GetWarningIssues())
            {
                warnings.AppendLine(warning.Message);
            }

            StringBuilder errors = new StringBuilder("errors :");
            foreach (var error in result.GetErrorIssues())
            {
                errors.AppendLine(error.Message);
            }
            return warnings.ToString() + "\n" + errors.ToString();
        }
    }
}
#endif
