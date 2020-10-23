// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class PackNewDefaultProjectTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackNewDefaultProjectTests(MsbuildIntegrationTestFixture fixture)
        {
            msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_IncludeSymbolsWithSnupkg()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"--include-symbols /p:SymbolPackageFormat=snupkg -o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var symbolPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.snupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(symbolPath), "The output .snupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                using (var symbolReader = new PackageArchiveReader(symbolPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    var libSymItems = symbolReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(1, libSymItems.Count);
                    Assert.Equal(symbolReader.GetPackageTypes().Count, 1);
                    Assert.Equal(symbolReader.GetPackageTypes()[0], NuGet.Packaging.Core.PackageType.SymbolsPackage);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.dll" }, libItems[0].Items);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.pdb" }, libSymItems[0].Items);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_NupkgExists()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
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
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(0, packages.Count);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.dll" }, libItems[0].Items);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_InstallPackageToOutputPath()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory} /p:OutputFileNamesWithoutVersion=true /p:InstallPackageToOutputPath=true");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.nupkg");
                var nupkgSha512Path = Path.Combine(workingDirectory, $"{projectName}.nupkg.sha512");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nupkgSha512Path), "The output .sha512 is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(0, packages.Count);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.dll" }, libItems[0].Items);
                }
            }
        }

        // This test checks to see that when IncludeBuildOutput=false, the generated nupkg does not contain lib folder
        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_IncludeBuildOutputDoesNotCreateLibFolder()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                using (var stream = new FileStream(Path.Combine(workingDirectory, $"{projectName}.csproj"), FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "9.0.1";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.Json",
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:IncludeBuildOutput=false");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("Newtonsoft.Json", packages[0].Id);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(0, libItems.Count);
                }
            }
        }

        // This test checks to see that when BuildOutputTargetFolder is specified, the generated nupkg has the DLLs in the specified output folder
        // instead of the default lib folder.
        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_BuildOutputTargetFolderOutputsLibsToRightFolder()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var buildOutputTargetFolder = "build";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:BuildOutputTargetFolder={buildOutputTargetFolder}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(0, libItems.Count);
                    libItems = nupkgReader.GetItems(buildOutputTargetFolder).ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(new[] { $"{buildOutputTargetFolder}/netstandard2.0/ClassLibrary1.dll" },
                        libItems[0].Items);
                }
            }
        }
    }
}
