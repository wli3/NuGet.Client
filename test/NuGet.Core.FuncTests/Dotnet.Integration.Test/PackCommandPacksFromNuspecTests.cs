// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class PackCommandPacksFromNuspecTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackCommandPacksFromNuspecTests(MsbuildIntegrationTestFixture fixture)
        {
            msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_PacksFromNuspec()
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>PackedFromNuspec</id>
    <version>1.2.1</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <description>This was packed from nuspec</description>
  </metadata>
  <files>
    <file src=""abc.txt"" target=""CoreCLR/"" />
  </files>
</package>";
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:NuspecFile=input.nuspec");

                var nupkgPath = Path.Combine(workingDirectory, $"PackedFromNuspec.1.2.1.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal("PackedFromNuspec", nuspecReader.GetId());
                    Assert.Equal("1.2.1", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("Microsoft", nuspecReader.GetAuthors());
                    Assert.Equal("NuGet", nuspecReader.GetOwners());
                    Assert.Equal("This was packed from nuspec", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("CoreCLR").ToArray();
                    Assert.Equal("CoreCLR/abc.txt", libItems[0]);
                }

            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_PacksFromNuspec_InstallPackageToOutputPath()
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>PackedFromNuspec</id>
    <version>1.2.1</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <description>This was packed from nuspec</description>
  </metadata>
</package>";
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);

                msbuildFixture.PackProject(
                    workingDirectory,
                    projectName,
                    $"-o {workingDirectory} /p:NuspecFile=input.nuspec /p:OutputFileNamesWithoutVersion=true /p:InstallPackageToOutputPath=true");

                var nuspecFilePath = Path.Combine(workingDirectory, "PackedFromNuspec.nuspec");
                var nupackageFilePath = Path.Combine(workingDirectory, "PackedFromNuspec.nupkg");
                var nupackageSha512FilePath = Path.Combine(workingDirectory, "PackedFromNuspec.nupkg.sha512");
                Assert.True(File.Exists(nuspecFilePath), "The output .nuspec is not in the expected place: " + nuspecFilePath);
                Assert.True(File.Exists(nupackageFilePath), "The output .nupkg is not in the expected place: " + nupackageFilePath);
                Assert.True(File.Exists(nupackageSha512FilePath), "The output .sha512 is not in the expected place: " + nupackageSha512FilePath);

                using (var nupkgReader = new PackageArchiveReader(nupackageFilePath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal("PackedFromNuspec", nuspecReader.GetId());
                    Assert.Equal("1.2.1", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("Microsoft", nuspecReader.GetAuthors());
                    Assert.Equal("NuGet", nuspecReader.GetOwners());
                    Assert.Equal("This was packed from nuspec", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;tags=tag1;description="hello world"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;tags=tag1;description=\"hello world\"\\\"", "MyPackage",
            "1.2.3", "hello world", "tag1")]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;tasg=tag1,tag2;description=""\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;tags=tag1,tag2;description=\"hello world\"\\\"", "MyPackage",
            "1.2.3", "hello world", "tag1,tag2")]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;tags=;description="hello = world"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;tags=;description=\"hello = world\"\\\"", "MyPackage",
            "1.2.3", "hello = world", "")]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;tags="";description="hello = world with a %3B"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;tags=\"\";description=\"hello = world with a %3B\"\\\"",
            "MyPackage", "1.2.3", "hello = world with a ;", "")]
        public void PackCommand_PackProject_PacksFromNuspecWithTokenSubstitution(
            string nuspecProperties,
            string expectedId,
            string expectedVersion,
            string expectedDescription,
            string expectedTags
            )
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>$id$</id>
    <version>$version$</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <tags>$tags$</tags>
    <description>$description$</description>
  </metadata>
  <files>
    <file src=""abc.txt"" target=""CoreCLR/"" />
  </files>
</package>";
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:NuspecFile=input.nuspec " + nuspecProperties);

                var nupkgPath = Path.Combine(workingDirectory, $"{expectedId}.{expectedVersion}.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal(expectedId, nuspecReader.GetId());
                    Assert.Equal(expectedVersion, nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("Microsoft", nuspecReader.GetAuthors());
                    Assert.Equal("NuGet", nuspecReader.GetOwners());
                    Assert.Equal(expectedDescription, nuspecReader.GetDescription());
                    Assert.Equal(expectedTags, nuspecReader.GetTags());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("CoreCLR").ToArray();
                    Assert.Equal("CoreCLR/abc.txt", libItems[0]);
                }

            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_PacksFromNuspecWithBasePath()
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>PackedFromNuspec</id>
    <version>1.2.1</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <description>This was packed from nuspec</description>
  </metadata>
  <files>
    <file src=""abc.txt"" target=""CoreCLR/"" />
  </files>
</package>";
            using (var basePathDirectory = msbuildFixture.CreateTestDirectory())
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(basePathDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:NuspecFile=input.nuspec /p:NuspecBasePath={basePathDirectory.Path}");

                var nupkgPath = Path.Combine(workingDirectory, $"PackedFromNuspec.1.2.1.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("CoreCLR").ToArray();
                    Assert.Equal("CoreCLR/abc.txt", libItems[0]);
                }

            }
        }

        [PlatformFact(Platform.Windows)]
        // This test is asserting that nuspec can be packed via dotnet.exe
        // without having to specify IncludeBuildOutput=false when using the
        // --no-build switch.
        public void PackCommand_PackProject_PackNuspecWithoutBuild()
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>PackedFromNuspec</id>
    <version>1.2.1</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <description>This was packed from nuspec</description>
  </metadata>
  <files>
    <file src=""abc.txt"" target=""CoreCLR/"" />
  </files>
</package>";
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.BuildProject(workingDirectory, projectName, "/restore");
                File.WriteAllText(Path.Combine(workingDirectory, "abc.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "sample text");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "NuspecFile", "abc.nuspec");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} --no-build");

                var nupkgPath = Path.Combine(workingDirectory, $"PackedFromNuspec.1.2.1.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal("PackedFromNuspec", nuspecReader.GetId());
                    Assert.Equal("1.2.1", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("Microsoft", nuspecReader.GetAuthors());
                    Assert.Equal("NuGet", nuspecReader.GetOwners());
                    Assert.Equal("This was packed from nuspec", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("CoreCLR").ToArray();
                    Assert.Equal("CoreCLR/abc.txt", libItems[0]);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_EmptyNuspecFilePropertyWithNuspecProperties()
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
                    ProjectFileUtils.AddProperty(xml, "NuspecProperties", "token1=value1");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:NuspecFile=");

                var nupkgPath = Path.Combine(workingDirectory, $"ClassLibrary1.1.0.0.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

            }
        }
    }
}
