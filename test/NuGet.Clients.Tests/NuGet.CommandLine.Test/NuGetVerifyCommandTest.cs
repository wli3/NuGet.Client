// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetVerifyCommandTest
    {
        private const int FailureCode = 1;
        private const int SuccessCode = 0;

        [Fact]
        public void VerifyCommand_WithHelpWhenMinArgsNotSatisfied_DoesNotRaiseError()
        {
            CommandAttribute attribute = typeof(VerifyCommand).GetCustomAttribute<CommandAttribute>();

            Assert.NotNull(attribute);

            // The "-help" option should display help without erroring out, even when
            // a command's "minimum arguments" requirement is not satisfied.
            Assert.Equal(1, attribute.MinArgs);

            string nugetExeFilePath = Util.GetNuGetExePath();

            using (TestDirectory workingDirectory = TestDirectory.Create())
            {
                var args = new string[] { "verify", "-help" };

                CommandRunnerResult result = CommandRunner.Run(
                    nugetExeFilePath,
                    workingDirectory,
                    string.Join(" ", args),
                    waitForExit: true);

                Assert.True(result.Success);
                Assert.Equal(0, result.ExitCode);
                Assert.StartsWith("usage: NuGet verify", result.Output);
                Assert.DoesNotContain("verify: invalid arguments.", result.Errors);
            }
        }

        [Fact]
        public void VerifyCommand_WhenVerificationTypeIsUnknown_Fails()
        {
            string nugetExeFilePath = Util.GetNuGetExePath();

            using (TestDirectory packageDirectory = TestDirectory.Create())
            {
                // Arrange
                string packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", packageDirectory);

                // Act
                var args = new string[] { "verify", packageFileName };
                CommandRunnerResult result = CommandRunner.Run(
                    nugetExeFilePath,
                    packageDirectory,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(FailureCode, result.ExitCode);
                Assert.Contains("Verification type not supported.", result.Errors);
            }
        }

        [Fact]
        public void VerifyCommand_WrongInput_NotFound()
        {
            string nugetExeFilePath = Util.GetNuGetExePath();

            using (TestDirectory packageDirectory = TestDirectory.Create())
            {
                // Act
                var args = new string[] { "verify", "-Signatures", "testPackage1" };
                CommandRunnerResult result = CommandRunner.Run(
                    nugetExeFilePath,
                    packageDirectory,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(FailureCode, result.ExitCode);
                Assert.Contains("File does not exist", result.Errors);
            }
        }

        [PlatformFact(Platform.Windows, SkipMono = true)]
        public void VerifyCommand_WithAuthorSignedPackage_FailsGracefully()
        {
            string nugetExeFilePath = Util.GetNuGetExePath();

            using (TestDirectory directory = TestDirectory.Create())
            {
                var packageFile = new FileInfo(Path.Combine(directory.Path, "TestPackage.AuthorSigned.1.0.0.nupkg"));
                byte[] package = GetResource(packageFile.Name);

                File.WriteAllBytes(packageFile.FullName, package);

                var args = new string[] { "verify", "-Signatures", packageFile.Name };
                CommandRunnerResult result = CommandRunner.Run(
                    nugetExeFilePath,
                    packageFile.Directory.FullName,
                    string.Join(" ", args),
                    waitForExit: true);

                if (RuntimeEnvironmentHelper.IsMono)
                {
                    Assert.True(FailureCode == result.ExitCode, result.AllOutput);
                    Assert.False(result.Success);
                    Assert.Contains("NU3004: The package is not signed.", result.AllOutput);
                }
                else
                {
                    Assert.True(SuccessCode == result.ExitCode, result.AllOutput);
                    Assert.True(result.Success);
                    Assert.Contains("Successfully verified package 'TestPackage.AuthorSigned.1.0.0'", result.AllOutput);
                }
            }
        }

        [Theory]
        [InlineData("verify")]
        [InlineData("verify a b")]
        public void VerifyCommand_Failure_InvalidArguments(string cmd)
        {
            Util.TestCommandInvalidArguments(cmd);
        }

        private static byte[] GetResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"NuGet.CommandLine.Test.compiler.resources.{name}",
                typeof(NuGetVerifyCommandTest));
        }
    }
}
