// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class PackCommandPackageIconTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackCommandPackageIconTests(MsbuildIntegrationTestFixture fixture)
        {
            msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackageIcon_HappyPath_Warns_Succeeds()
        {
            var testDirBuilder = TestDirectoryBuilder.Create();
            var projectBuilder = ProjectFileBuilder.Create();

            testDirBuilder
                .WithFile("test\\folder\\notes.txt", 10)
                .WithFile("test\\folder\\nested\\content.txt", 10)
                .WithFile("test\\folder\\nested\\sample.txt", 10)
                .WithFile("test\\folder\\nested\\media\\readme.txt", 10)
                .WithFile("test\\icon.jpg", 10)
                .WithFile("test\\other\\files.txt", 10)
                .WithFile("test\\utils\\sources.txt", 10);

            projectBuilder
                .WithProjectName("test")
                .WithPackageIcon("icon.jpg")
                .WithPackageIconUrl("http://test.icon")
                .WithItem(itemType: "None", itemPath: "icon.jpg", packagePath: string.Empty, pack: "true")
                .WithItem(itemType: "None", itemPath: "other\\files.txt", packagePath: null, pack: "true")
                .WithItem(itemType: "None", itemPath: "folder\\**", packagePath: "media", pack: "true")
                .WithItem(itemType: "None", itemPath: "utils\\*", packagePath: "utils", pack: "true");

            using (var srcDir = msbuildFixture.Build(testDirBuilder))
            {
                projectBuilder.Build(msbuildFixture, srcDir.Path);
                var result = msbuildFixture.PackProject(projectBuilder.ProjectFolder, projectBuilder.ProjectName, string.Empty);

                // Validate embedded icon in package
                ValidatePackIcon(projectBuilder);

                // Validate that other content is also included
                var nupkgPath = Path.Combine(projectBuilder.ProjectFolder, "bin", "Debug", $"{projectBuilder.ProjectName}.1.0.0.nupkg");
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    Assert.NotNull(nupkgReader.GetEntry("content/other/files.txt"));
                    Assert.NotNull(nupkgReader.GetEntry("utils/sources.txt"));
                    Assert.NotNull(nupkgReader.GetEntry("media/nested/sample.txt"));
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackageIcon_MissingFile_Fails()
        {
            var testDirBuilder = TestDirectoryBuilder.Create();
            var projectBuilder = ProjectFileBuilder.Create();

            projectBuilder
                .WithProjectName("test")
                .WithPackageIcon("icon.jpg");

            using (var srcDir = msbuildFixture.Build(testDirBuilder))
            {
                projectBuilder.Build(msbuildFixture, srcDir.Path);
                var result = msbuildFixture.PackProject(projectBuilder.ProjectFolder, projectBuilder.ProjectName, string.Empty, validateSuccess: false);

                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains(NuGetLogCode.NU5046.ToString(), result.Output);
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("snupkg")]
        [InlineData("symbols.nupkg")]
        public void PackCommand_PackageIcon_PackWithSymbols_Succeeds(string symbolPackageFormat)
        {
            var testDirBuilder = TestDirectoryBuilder.Create();
            var projectBuilder = ProjectFileBuilder.Create();

            testDirBuilder
                .WithFile("test\\icon.jpg", 10);

            projectBuilder
                .WithProjectName("test")
                .WithPackageIcon("icon.jpg")
                .WithItem(itemType: "None", itemPath: "icon.jpg", packagePath: "icon.jpg", pack: "true");

            using (var srcDir = msbuildFixture.Build(testDirBuilder))
            {
                projectBuilder.Build(msbuildFixture, srcDir.Path);
                var result = msbuildFixture.PackProject(
                    projectBuilder.ProjectFolder,
                    projectBuilder.ProjectName,
                    $"--include-symbols /p:SymbolPackageFormat={symbolPackageFormat}");
            }
        }

        private void ValidatePackIcon(ProjectFileBuilder projectBuilder)
        {
            Assert.True(File.Exists(projectBuilder.ProjectFilePath), "No project was produced");
            var nupkgPath = Path.Combine(projectBuilder.ProjectFolder, "bin", "Debug", $"{projectBuilder.ProjectName}.1.0.0.nupkg");

            Assert.True(File.Exists(nupkgPath), "No package was produced");

            using (var nupkgReader = new PackageArchiveReader(nupkgPath))
            {
                var nuspecReader = nupkgReader.NuspecReader;

                Assert.Equal(projectBuilder.PackageIcon, nuspecReader.GetIcon());
            }
        }
    }
}
