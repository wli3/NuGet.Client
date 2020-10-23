// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class PackCommandSourceControlInformationTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackCommandSourceControlInformationTests(MsbuildIntegrationTestFixture fixture)
        {
            msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackWithSourceControlInformation_Unsupported_VerifyNuspec()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var ns = xml.Root.Name.Namespace;

                    ProjectFileUtils.AddProperty(xml, "RepositoryType", "git");

                    // mock implementation of InitializeSourceControlInformation common targets:
                    xml.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "e1c65e4524cd70ee6e22abe33e6cb6ec73938cb1"),
                                new XElement("PrivateRepositoryUrl", "https://github.com/NuGet/NuGet.Client.git"))));

                    xml.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "false")));

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    var repositoryMetadata = nuspecReader.GetRepositoryMetadata();
                    repositoryMetadata.Type.Should().Be("git");
                    repositoryMetadata.Url.Should().Be("");
                    repositoryMetadata.Branch.Should().Be("");
                    repositoryMetadata.Commit.Should().Be("");
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackWithSourceControlInformation_PrivateUrl_VerifyNuspec()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var ns = xml.Root.Name.Namespace;

                    ProjectFileUtils.AddProperty(xml, "RepositoryType", "git");

                    xml.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true")));

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // mock implementation of InitializeSourceControlInformation common targets:
                var mockXml = @"<Project>
<Target Name=""InitializeSourceControlInformation"">
    <PropertyGroup>
      <SourceRevisionId>e1c65e4524cd70ee6e22abe33e6cb6ec73938cb1</SourceRevisionId>
      <PrivateRepositoryUrl>https://github.com/NuGet/NuGet.Client.git</PrivateRepositoryUrl>
    </PropertyGroup>
</Target>
</Project>";

                File.WriteAllText(Path.Combine(workingDirectory, "Directory.build.targets"), mockXml);


                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    var repositoryMetadata = nuspecReader.GetRepositoryMetadata();
                    repositoryMetadata.Type.Should().Be("git");
                    repositoryMetadata.Url.Should().Be("");
                    repositoryMetadata.Branch.Should().Be("");
                    repositoryMetadata.Commit.Should().Be("e1c65e4524cd70ee6e22abe33e6cb6ec73938cb1");
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackWithSourceControlInformation_PublishedUrl_VerifyNuspec()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var ns = xml.Root.Name.Namespace;

                    ProjectFileUtils.AddProperty(xml, "RepositoryType", "git");
                    ProjectFileUtils.AddProperty(xml, "PublishRepositoryUrl", "true");

                    xml.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true")));

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // mock implementation of InitializeSourceControlInformation common targets:
                var mockXml = @"<Project>
<Target Name=""InitializeSourceControlInformation"">
    <PropertyGroup>
      <SourceRevisionId>e1c65e4524cd70ee6e22abe33e6cb6ec73938cb1</SourceRevisionId>
      <PrivateRepositoryUrl>https://github.com/NuGet/NuGet.Client.git</PrivateRepositoryUrl>
    </PropertyGroup>
</Target>
</Project>";

                File.WriteAllText(Path.Combine(workingDirectory, "Directory.build.targets"), mockXml);

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    var repositoryMetadata = nuspecReader.GetRepositoryMetadata();
                    repositoryMetadata.Type.Should().Be("git");
                    repositoryMetadata.Url.Should().Be("https://github.com/NuGet/NuGet.Client.git");
                    repositoryMetadata.Branch.Should().Be("");
                    repositoryMetadata.Commit.Should().Be("e1c65e4524cd70ee6e22abe33e6cb6ec73938cb1");
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackWithSourceControlInformation_ProjectOverride_VerifyNuspec()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var ns = xml.Root.Name.Namespace;

                    ProjectFileUtils.AddProperty(xml, "RepositoryType", "git");
                    ProjectFileUtils.AddProperty(xml, "PublishRepositoryUrl", "true");
                    ProjectFileUtils.AddProperty(xml, "RepositoryCommit", "1111111111111111111111111111111111111111");
                    ProjectFileUtils.AddProperty(xml, "RepositoryUrl", "https://github.com/Overridden");

                    // mock implementation of InitializeSourceControlInformation common targets:
                    xml.Root.Add(
                        new XElement(ns + "Target",
                            new XAttribute("Name", "InitializeSourceControlInformation"),
                            new XElement(ns + "PropertyGroup",
                                new XElement("SourceRevisionId", "e1c65e4524cd70ee6e22abe33e6cb6ec73938cb1"),
                                new XElement("PrivateRepositoryUrl", "https://github.com/NuGet/NuGet.Client"))));

                    xml.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement("SourceControlInformationFeatureSupported", "true")));

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    var repositoryMetadata = nuspecReader.GetRepositoryMetadata();
                    repositoryMetadata.Type.Should().Be("git");
                    repositoryMetadata.Url.Should().Be("https://github.com/Overridden");
                    repositoryMetadata.Branch.Should().Be("");
                    repositoryMetadata.Commit.Should().Be("1111111111111111111111111111111111111111");
                }
            }
        }
    }
}
