// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class PackCommandTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackCommandTests(MsbuildIntegrationTestFixture fixture)
        {
            msbuildFixture = fixture;
        }



        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackToolUsingAlias_DoesNotWarnAboutNoExactMatchInDependencyGroupAndLibRefDirectories()
        {
            // Ref: https://github.com/NuGet/Home/issues/10097
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Arrange
                var projectName = "ConsoleApp1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " console");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.AddProperty(xml, "PackAsTool", "true");
                    ProjectFileUtils.ChangeProperty(xml, "TargetFramework", "myalias");

                    var tfmProps = new Dictionary<string, string>();
                    tfmProps["TargetFrameworkIdentifier"] = ".NETCoreApp";
                    tfmProps["TargetFrameworkVersion"] = "v3.1";
                    tfmProps["TargetFrameworkMoniker"] = ".NETCoreApp,Version=v3.1";
                    ProjectFileUtils.AddProperties(xml, tfmProps, " '$(TargetFramework)' == 'myalias' ");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // Act
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                result.AllOutput.Should().NotContain("NU5128");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProjectWithPackageType_SnupkgContainsOnlyOnePackageType()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageType", NuGet.Packaging.Core.PackageType.Dependency.Name);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

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
                    Assert.Equal(nupkgReader.GetPackageTypes().Count, 1);
                    Assert.Equal(nupkgReader.GetPackageTypes()[0], NuGet.Packaging.Core.PackageType.Dependency);
                    Assert.Equal(symbolReader.GetPackageTypes().Count, 1);
                    Assert.Equal(symbolReader.GetPackageTypes()[0], NuGet.Packaging.Core.PackageType.SymbolsPackage);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.dll" }, libItems[0].Items);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.pdb" }, libSymItems[0].Items);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public void PackCommand_PackConsoleAppWithRID_NupkgValid(bool includeSymbols)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ConsoleApp1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " console");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.ChangeProperty(xml, "TargetFramework", "netcoreapp2.1");
                    ProjectFileUtils.AddProperty(xml, "RuntimeIdentifier", "win7-x64");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var args = includeSymbols ? $"-o {workingDirectory} --include-symbols" : $"-o {workingDirectory}";
                msbuildFixture.PackProject(workingDirectory, projectName, args);

                var nupkgExtension = includeSymbols ? ".symbols.nupkg" : ".nupkg";
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0{nupkgExtension}");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), $"The output {nupkgExtension} is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp21, libItems[0].TargetFramework);
                    if (includeSymbols)
                    {
                        Assert.Equal(new[]
                        {
                            "lib/netcoreapp2.1/ConsoleApp1.dll",
                            "lib/netcoreapp2.1/ConsoleApp1.pdb",
                            "lib/netcoreapp2.1/ConsoleApp1.runtimeconfig.json"
                        }, libItems[0].Items);
                    }
                    else
                    {
                        Assert.Equal(new[]
                        {
                            "lib/netcoreapp2.1/ConsoleApp1.dll",
                            "lib/netcoreapp2.1/ConsoleApp1.runtimeconfig.json"
                        }, libItems[0].Items);
                    }
                }

            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_PackageReferenceFloatingVersionRange()
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net45");

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "(10.0.*,11.0.1]";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.Json",
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(1,
                        dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[0].TargetFramework);
                    var packagesB = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packagesB.Count);
                    Assert.Equal("Newtonsoft.Json", packagesB[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("10.0.3"), false, new NuGetVersion("11.0.1"), true), packagesB[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesB[0].Exclude);
                    Assert.Empty(packagesB[0].Include);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task PackCommand_PackProject_PackageReferenceAllStableFloatingVersionRange_UsesRestoredVersionInNuspecAsync()
        {
            // Arrange
            using (var pathContext = msbuildFixture.CreateSimpleTestPathContext())
            {
                var projectName = "ClassLibrary1";
                var availableVersions = "1.0.0;2.0.0";
                var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(pathContext.SolutionRoot, projectName);

                foreach (string version in availableVersions.Split(';'))
                {
                    // Set up the package and source
                    var package = new SimpleTestPackageContext()
                    {
                        Id = "x",
                        Version = version
                    };

                    package.Files.Clear();
                    package.AddFile($"lib/net45/a.dll");

                    await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                         pathContext.PackageSource,
                         PackageSaveMode.Defaultv3,
                         package);
                }

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net45");

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "*";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "x",
                        string.Empty,
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(1,
                        dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[0].TargetFramework);
                    var packagesB = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packagesB.Count);
                    Assert.Equal("x", packagesB[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("2.0.0"), true, null, false), packagesB[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesB[0].Exclude);
                    Assert.Empty(packagesB[0].Include);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_SupportMultipleFrameworks()
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "netcoreapp1.0;net45");

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "9.0.1";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.Json",
                        "net45",
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(2,
                        dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10,
                        dependencyGroups[0].TargetFramework);
                    var packagesA = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1,
                        packagesA.Count);
                    Assert.Equal("Microsoft.NETCore.App", packagesA[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.0.5")), packagesA[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[1].TargetFramework);
                    var packagesB = dependencyGroups[1].Packages.ToList();
                    Assert.Equal(1, packagesB.Count);
                    Assert.Equal("Newtonsoft.Json", packagesB[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("9.0.1")), packagesB[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesB[0].Exclude);
                    Assert.Empty(packagesB[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(2, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10, libItems[0].TargetFramework);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[1].TargetFramework);
                    Assert.Equal(
                        new[]
                        {"lib/netcoreapp1.0/ClassLibrary1.dll", "lib/netcoreapp1.0/ClassLibrary1.runtimeconfig.json"},
                        libItems[0].Items);
                    Assert.Equal(new[] { "lib/net45/ClassLibrary1.exe" },
                        libItems[1].Items);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(null, null, null, true, "", "Analyzers,Build")]
        [InlineData(null, "Native", null, true, "", "Analyzers,Build,Native")]
        [InlineData("Compile", null, null, true, "", "Analyzers,Build,BuildTransitive,Native,Runtime")]
        [InlineData("Compile;Runtime", null, null, true, "", "Analyzers,Build,BuildTransitive,Native")]
        [InlineData("All", null, "None", true, "All", "")]
        [InlineData("All", null, "Compile", true, "Analyzers,Build,BuildTransitive,ContentFiles,Native,Runtime", "")]
        [InlineData("All", null, "Compile;Build", true, "Analyzers,BuildTransitive,ContentFiles,Native,Runtime", "")]
        [InlineData("All", "Native", "Compile;Build", true, "Analyzers,BuildTransitive,ContentFiles,Runtime", "")]
        [InlineData("All", "Native", "Native;Build", true, "Analyzers,BuildTransitive,Compile,ContentFiles,Runtime", "")]
        [InlineData("Compile", "Native", "Native;Build", true, "", "Analyzers,Build,BuildTransitive,Native,Runtime")]
        [InlineData("All", "All", null, false, null, null)]
        [InlineData("Compile;Runtime", "All", null, false, null, null)]
        [InlineData(null, null, "All", false, null, null)]
        public void PackCommand_SupportsIncludeExcludePrivateAssets_OnPackages(
            string includeAssets,
            string excludeAssets,
            string privateAssets,
            bool hasPackage,
            string expectedInclude,
            string expectedExclude)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net45");

                    var attributes = new Dictionary<string, string>();
                    attributes["Version"] = "9.0.1";

                    var properties = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(includeAssets))
                    {
                        properties["IncludeAssets"] = includeAssets;
                    }
                    if (!string.IsNullOrEmpty(excludeAssets))
                    {
                        properties["ExcludeAssets"] = excludeAssets;
                    }
                    if (!string.IsNullOrEmpty(privateAssets))
                    {
                        properties["PrivateAssets"] = privateAssets;
                    }

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.Json",
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var package = nuspecReader
                        .GetDependencyGroups()
                        .SingleOrDefault()?
                        .Packages
                        .SingleOrDefault();

                    if (!hasPackage)
                    {
                        Assert.Null(package);
                    }
                    else
                    {
                        Assert.NotNull(package);
                        Assert.Equal(expectedInclude, string.Join(",", package.Include));
                        Assert.Equal(expectedExclude, string.Join(",", package.Exclude));
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_AddsProjectRefsAsPackageRefs()
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var referencedProject = "ClassLibrary2";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, "console");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, referencedProject, "classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        @"..\ClassLibrary2\ClassLibrary2.csproj",
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(1,
                        dependencyGroups.Count);

                    var packagesA = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1,
                        packagesA.Count);

                    Assert.Equal("ClassLibrary2", packagesA[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.0.0")), packagesA[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    libItems.Should().HaveCount(1);
                    var files = libItems[0].Items;
                    files.Should().HaveCount(2);
                    files.Should().ContainSingle(filePath => filePath.Contains("ClassLibrary1.runtimeconfig.json"));
                    files.Should().ContainSingle(filePath => filePath.Contains("ClassLibrary1.dll"));
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
#if NETCOREAPP5_0
        [InlineData("TargetFramework", "net5.0")]
        [InlineData("TargetFrameworks", "netstandard1.4;net5.0")]
#endif
        public void PackCommand_PackProject_ExactVersionOverrideProjectRefVersionInMsbuild(string tfmProperty, string tfmValue)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var referencedProject = "ClassLibrary2";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var refProjectFile = Path.Combine(testDirectory, referencedProject, $"{referencedProject}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, referencedProject, "classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                using (var refStream = new FileStream(refProjectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    var target = @"<Target Name=""_ExactProjectReferencesVersion"" AfterTargets=""_GetProjectReferenceVersions"">
    <ItemGroup>
      <_ProjectReferencesWithExactVersions Include=""@(_ProjectReferencesWithVersions)"">
        <ProjectVersion>[%(_ProjectReferencesWithVersions.ProjectVersion)]</ProjectVersion>
      </_ProjectReferencesWithExactVersions>
    </ItemGroup>

    <ItemGroup>
      <_ProjectReferencesWithVersions Remove=""@(_ProjectReferencesWithVersions)"" />
      <_ProjectReferencesWithVersions Include=""@(_ProjectReferencesWithExactVersions)"" />
    </ItemGroup>
  </Target>";
                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        @"..\ClassLibrary2\ClassLibrary2.csproj",
                        string.Empty,
                        properties,
                        attributes);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddCustomXmlToProjectRoot(xml, target);

                    var refXml = XDocument.Load(refStream);
                    ProjectFileUtils.AddProperty(refXml, "PackageVersion", "1.2.3-alpha", "'$(ExcludeRestorePackageImports)' != 'true'");
                    ProjectFileUtils.SetTargetFrameworkForProject(refXml, tfmProperty, tfmValue);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                    ProjectFileUtils.WriteXmlToFile(refXml, refStream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(tfmValue.Split(';').Count(),
                        dependencyGroups.Count);
                    foreach (var depGroup in dependencyGroups)
                    {
                        var packages = depGroup.Packages.ToList();
                        var package = packages.Where(t => t.Id.Equals("ClassLibrary2")).First();
                        Assert.Equal(new VersionRange(new NuGetVersion("1.2.3-alpha"), true, new NuGetVersion("1.2.3-alpha"), true), package.VersionRange);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
#if NETCOREAPP5_0
        [InlineData("TargetFramework", "net5.0")]
        [InlineData("TargetFrameworks", "netstandard1.4;net5.0")]
#endif
        public void PackCommand_PackProject_GetsProjectRefVersionFromMsbuild(string tfmProperty, string tfmValue)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var referencedProject = "ClassLibrary2";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var refProjectFile = Path.Combine(testDirectory, referencedProject, $"{referencedProject}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, referencedProject, "classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                using (var refStream = new FileStream(refProjectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        @"..\ClassLibrary2\ClassLibrary2.csproj",
                        string.Empty,
                        properties,
                        attributes);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);

                    var refXml = XDocument.Load(refStream);
                    ProjectFileUtils.AddProperty(refXml, "PackageVersion", "1.2.3-alpha", "'$(ExcludeRestorePackageImports)' != 'true'");
                    ProjectFileUtils.SetTargetFrameworkForProject(refXml, tfmProperty, tfmValue);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                    ProjectFileUtils.WriteXmlToFile(refXml, refStream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(tfmValue.Split(';').Count(),
                        dependencyGroups.Count);
                    foreach (var depGroup in dependencyGroups)
                    {
                        var packages = depGroup.Packages.ToList();
                        var package = packages.Where(t => t.Id.Equals("ClassLibrary2")).First();
                        Assert.Equal(new VersionRange(new NuGetVersion("1.2.3-alpha")), package.VersionRange);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
#if NETCOREAPP5_0
        [InlineData("TargetFramework", "net5.0")]
        [InlineData("TargetFrameworks", "netstandard1.4;net5.0")]
#endif
        public void PackCommand_PackProject_GetPackageVersionDependsOnWorks(string tfmProperty, string tfmValue)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var referencedProject = "ClassLibrary2";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var refProjectFile = Path.Combine(testDirectory, referencedProject, $"{referencedProject}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, referencedProject, "classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                using (var refStream = new FileStream(refProjectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    var target = @"<Target Name=""CalculatePackageVersion"">
    <PropertyGroup>
      <PackageVersion>1.2.3-alpha</PackageVersion>
    </PropertyGroup>
  </Target>";
                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        @"..\ClassLibrary2\ClassLibrary2.csproj",
                        string.Empty,
                        properties,
                        attributes);
                    ProjectFileUtils.AddProperty(xml, "GetPackageVersionDependsOn", "CalculatePackageVersion");
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddCustomXmlToProjectRoot(xml, target);

                    var refXml = XDocument.Load(refStream);
                    ProjectFileUtils.AddProperty(refXml, "GetPackageVersionDependsOn", "CalculatePackageVersion");
                    ProjectFileUtils.SetTargetFrameworkForProject(refXml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddCustomXmlToProjectRoot(refXml, target);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                    ProjectFileUtils.WriteXmlToFile(refXml, refStream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.2.3-alpha.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.2.3-alpha.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(tfmValue.Split(';').Count(),
                        dependencyGroups.Count);
                    foreach (var depGroup in dependencyGroups)
                    {
                        var packages = depGroup.Packages.ToList();
                        var package = packages.Where(t => t.Id.Equals("ClassLibrary2")).First();
                        Assert.Equal(new VersionRange(new NuGetVersion("1.2.3-alpha")), package.VersionRange);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt", null, "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt", null,
            "content/folderA/folderB/abc.txt;contentFiles/any/netstandard1.4/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("abc.txt", "", "abc.txt")]
        [InlineData("abc.txt", "/", "abc.txt")]
        [InlineData("abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "", "abc.txt")]
        [InlineData("folderA/abc.txt", "/", "abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "", "abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "/", "abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("../abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("../abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("../abc.txt", "", "abc.txt")]
        [InlineData("../abc.txt", "/", "abc.txt")]
        [InlineData("../abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        // ## is a special syntax specifically for this test which means that ## should be replaced by the absolute path to the project directory.
        [InlineData("##/abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("##/folderA/abc.txt", null,
            "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("##/../abc.txt", null, "content/abc.txt")]
        [InlineData("##/abc.txt", "", "abc.txt")]
        [InlineData("##/abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/folderA/abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/../abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]

        public void PackCommand_PackProject_PackagePathPacksContentCorrectly(string sourcePath, string packagePath,
            string expectedTargetPaths)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }
                else if (sourcePath.StartsWith("{AbsolutePath}"))
                {
                    sourcePath = sourcePath.Replace("{AbsolutePath}", Path.GetTempPath().Replace('\\', '/'));
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    if (packagePath != null)
                    {
                        properties["PackagePath"] = packagePath;
                    }
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var pathToContent = string.Empty;
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                else
                {
                    pathToContent = Path.Combine(workingDirectory, sourcePath);
                }

                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var items = new HashSet<string>(nupkgReader.GetFiles());
                    var expectedPaths = expectedTargetPaths.Split(';');
                    foreach (var path in expectedPaths)
                    {
                        Assert.Contains(path, items);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(null, null, null, "1.0.0")]
        [InlineData("1.2.3", null, null, "1.2.3")]
        [InlineData(null, "rtm-1234", null, "1.0.0-rtm-1234")]
        [InlineData("1.2.3", "rtm-1234", null, "1.2.3-rtm-1234")]
        [InlineData(null, null, "2.3.1", "2.3.1")]
        [InlineData("1.2.3", null, "2.3.1", "2.3.1")]
        [InlineData(null, "rtm-1234", "2.3.1", "2.3.1")]
        [InlineData("1.2.3", "rtm-1234", "2.3.1", "2.3.1")]
        public void PackCommand_PackProject_OutputsCorrectVersion(string versionPrefix, string versionSuffix,
            string packageVersion, string expectedVersion)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                var args = "" +
                           (versionPrefix != null ? $" /p:VersionPrefix={versionPrefix} " : string.Empty) +
                           (versionSuffix != null ? $" /p:VersionSuffix={versionSuffix} " : string.Empty) +
                           (packageVersion != null ? $" /p:PackageVersion={packageVersion} " : string.Empty);
                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory} {args}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.{expectedVersion}.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.{expectedVersion}.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var intermediateNuspec = new NuspecReader(nuspecPath);
                    var nuspecReader = nupkgReader.NuspecReader;
                    Assert.Equal(expectedVersion, nuspecReader.GetVersion().ToFullString());
                    Assert.Equal(expectedVersion, intermediateNuspec.GetVersion().ToFullString());
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt", null, "any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt", null, "any/netstandard1.4/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("##/abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("##/folderA/abc.txt", null, "any/netstandard1.4/folderA/abc.txt")]
        [InlineData("##/../abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "contentFiles", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("../abc.txt", "contentFiles", "abc.txt")]
        [InlineData("##/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles", "abc.txt")]
        [InlineData("abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("../abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("##/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("../abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("##/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("../abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("##/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("##/../abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("abc.txt", "folderA", null)]
        [InlineData("folderA/abc.txt", "folderA", null)]
        [InlineData("folderA/folderB/abc.txt", "folderA", null)]
        [InlineData("../abc.txt", "folderA", null)]
        [InlineData("##/abc.txt", "folderA", null)]
        [InlineData("##/folderA/abc.txt", "folderA", null)]
        [InlineData("##/../abc.txt", "folderA", null)]
        [InlineData("abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("../abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("##/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        public void PackCommand_PackProject_OutputsContentFilesInNuspecForSingleFramework(string sourcePath,
            string packagePath, string expectedIncludeString)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));

                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                var pathToContent = Path.Combine(workingDirectory, sourcePath);
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    if (packagePath != null)
                    {
                        properties["PackagePath"] = packagePath;
                    }
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var contentFiles = nuspecReader.GetContentFiles().ToArray();

                    if (expectedIncludeString == null)
                    {
                        Assert.True(contentFiles.Count() == 0);
                    }
                    else
                    {
                        Assert.True(contentFiles.Count() == 1);
                        var contentFile = contentFiles[0];
                        Assert.Equal(expectedIncludeString, contentFile.Include);
                        Assert.Equal("Content", contentFile.BuildAction);

                        var files = nupkgReader.GetFiles("contentFiles");
                        Assert.Contains("contentFiles/" + expectedIncludeString, files);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        [InlineData("folderA/abc.txt", "any/net45/folderA/abc.txt;any/netstandard1.3/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt",
            "any/net45/folderA/folderB/abc.txt;any/netstandard1.3/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        [InlineData("##/abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        [InlineData("##/folderA/abc.txt", "any/net45/folderA/abc.txt;any/netstandard1.3/folderA/abc.txt")]
        [InlineData("##/../abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        public void PackCommand_PackProject_OutputsContentFilesInNuspecForMultipleFrameworks(string sourcePath,
            string expectedIncludeString)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));

                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                var pathToContent = Path.Combine(workingDirectory, sourcePath);
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "net45;netstandard1.3");

                    var attributes = new Dictionary<string, string>();
                    var properties = new Dictionary<string, string>();

                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var contentFiles = nuspecReader.GetContentFiles().ToArray();

                    if (expectedIncludeString == null)
                    {
                        Assert.True(contentFiles.Count() == 0);
                    }
                    else
                    {
                        var expectedStrings = expectedIncludeString.Split(';');
                        Assert.True(contentFiles.Count() == 2);
                        var contentFileSet = contentFiles.Select(p => p.Include);
                        var files = nupkgReader.GetFiles("contentFiles");
                        foreach (var expected in expectedStrings)
                        {
                            Assert.Contains(expected, contentFileSet);
                            Assert.Contains("contentFiles/" + expected, files);
                        }
                    }
                }
            }
        }

#if NETCOREAPP5_0
        [PlatformFact(Platform.Windows)]
        public void PackCommand_SingleFramework_GeneratesPackageOnBuildUsingNet5()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net5.0");
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");
                    ProjectFileUtils.AddProperty(xml, "NuspecOutputPath", "obj\\Debug");

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "9.0.1";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.json",
                        "",
                        new Dictionary<string, string>(),
                        attributes);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", "Debug", $"{projectName}.1.0.0.nuspec");

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
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net50, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("Newtonsoft.json", packages[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("9.0.1")), packages[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net50, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/net5.0/ClassLibrary1.dll" }, libItems[0].Items);
                }

            }
        }
#endif

        [PlatformFact(Platform.Windows)]
        public void PackCommand_SingleFramework_GeneratesPackageOnBuild()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");
                    ProjectFileUtils.AddProperty(xml, "NuspecOutputPath", "obj\\Debug");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", "Debug", $"{projectName}.1.0.0.nuspec");

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
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("NETStandard.Library", packages[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6.1")), packages[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard1.4/ClassLibrary1.dll" }, libItems[0].Items);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("netstandard1.4")]
        [InlineData("netstandard1.4;net451")]
        [InlineData("netstandard1.4;net451;netcoreapp1.0")]
        public void PackCommand_MultipleFrameworks_GeneratesPackageOnBuild(string frameworks)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", frameworks);
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");
                    ProjectFileUtils.AddProperty(xml, "NuspecOutputPath", "obj\\Debug");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", "Debug", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");


                var frameworksArray = frameworks.Split(';');
                var count = frameworksArray.Length;

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
                    Assert.Equal(count, dependencyGroups.Count);

                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("lib").ToList();
                    Assert.Equal(count, libItems.Count());

                    foreach (var framework in frameworksArray)
                    {
                        Assert.Contains($"lib/{framework}/ClassLibrary1.dll", libItems);
                    }
                }
            }
        }




        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt", null, "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt", null, "content/folderA/folderB/abc.txt;contentFiles/any/netstandard1.4/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("abc.txt", "folderA/xyz.txt", "folderA/xyz.txt/abc.txt")]
        [InlineData("abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA/", "folderA/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;folderB", "folderA/folderA/abc.txt;folderB/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles", "folderA/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles\\", "folderA/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles/", "folderA/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles/folderA", "folderA/folderA/abc.txt;contentFiles/folderA/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA/xyz.txt", "folderA/xyz.txt/folderA/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA/xyz.txt", "folderA/xyz.txt/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("{AbsolutePath}/abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("../abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("../abc.txt", "folderA/xyz.txt", "folderA/xyz.txt/abc.txt")]
        [InlineData("../abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        // ## is a special syntax specifically for this test which means that ## should be replaced by the absolute path to the project directory.
        [InlineData("##/abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("##/folderA/abc.txt", null, "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("##/../abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("##/abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/folderA/abc.txt", "folderX;folderY", "folderX/folderA/abc.txt;folderY/folderA/abc.txt")]
        [InlineData("##/../abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]

        public void PackCommand_PackProject_ContentTargetFoldersPacksContentCorrectly(string sourcePath,
            string contentTargetFolders, string expectedTargetPaths)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }
                else if (sourcePath.StartsWith("{AbsolutePath}"))
                {
                    sourcePath = sourcePath.Replace("{AbsolutePath}", Path.GetTempPath().Replace('\\', '/'));
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");
                    ProjectFileUtils.AddProperty(xml, "ContentTargetFolders", contentTargetFolders);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var pathToContent = string.Empty;
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                else
                {
                    pathToContent = Path.Combine(workingDirectory, sourcePath);
                }
                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var items = new HashSet<string>(nupkgReader.GetFiles());
                    var expectedPaths = expectedTargetPaths.Split(';');
                    foreach (var path in expectedPaths)
                    {
                        Assert.Contains(path, items);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
        public void PackCommand_IncludeSource_AddsSourceFiles(string tfmProperty, string tfmValue)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var utilitySrcFileContent = @"using System;
namespace ClassLibrary
{
    public class UtilityMethods
    {
    }
}";
                var extensionSrcFileContent = @"using System;
namespace ClassLibrary
{
    public class ExtensionMethods
    {
    }
}";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                Directory.CreateDirectory(Path.Combine(workingDirectory, "Utils"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "Extensions"));
                File.WriteAllText(Path.Combine(workingDirectory, "Utils", "Utility.cs"), utilitySrcFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "Extensions", "ExtensionMethods.cs"),
                    extensionSrcFileContent);

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"--include-source /p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var symbolsNupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.symbols.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(symbolsNupkgPath), "The output symbols nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(symbolsNupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    // Validate the assets.
                    var srcItems = nupkgReader.GetFiles("src").ToArray();
                    Assert.True(srcItems.Length == 4);
                    Assert.Contains("src/ClassLibrary1/ClassLibrary1.csproj", srcItems);
                    Assert.Contains("src/ClassLibrary1/Class1.cs", srcItems);
                    Assert.Contains("src/ClassLibrary1/Extensions/ExtensionMethods.cs", srcItems);
                    Assert.Contains("src/ClassLibrary1/Utils/Utility.cs", srcItems);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
        public void PackCommand_ContentInnerTargetExtension_AddsTfmSpecificContent(string tfmProperty, string tfmValue)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                Directory.CreateDirectory(Path.Combine(workingDirectory, "Extensions", "cs"));
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "hello world");
                File.WriteAllText(Path.Combine(workingDirectory, "Extensions", "ext.txt"), "hello world again");
                File.WriteAllText(Path.Combine(workingDirectory, "Extensions", "cs", "ext.cs.txt"), "hello world again");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var target = @"<Target Name=""CustomContentTarget"">
    <ItemGroup>
      <TfmSpecificPackageFile Include=""abc.txt"">
        <PackagePath>mycontent/$(TargetFramework)</PackagePath>
      </TfmSpecificPackageFile>
      <TfmSpecificPackageFile Include=""Extensions/ext.txt"" Condition=""'$(TargetFramework)' == 'net46'"">
        <PackagePath>net46content</PackagePath>
      </TfmSpecificPackageFile>
      <TfmSpecificPackageFile Include=""Extensions/**/ext.cs.txt"" Condition=""'$(TargetFramework)' == 'net46'"">
        <PackagePath>net46content</PackagePath>
      </TfmSpecificPackageFile>
    </ItemGroup>
  </Target>";
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddProperty(xml, "TargetsForTfmSpecificContentInPackage", "CustomContentTarget");
                    ProjectFileUtils.AddCustomXmlToProjectRoot(xml, target);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var files = nupkgReader.GetFiles("mycontent");
                    var tfms = tfmValue.Split(';');
                    Assert.Equal(tfms.Length, files.Count());

                    foreach (var tfm in tfms)
                    {
                        Assert.Contains($"mycontent/{tfm}/abc.txt", files);
                        var net46files = nupkgReader.GetFiles("net46content");
                        if (tfms.Length > 1)
                        {
                            Assert.Equal(2, net46files.Count());
                            Assert.Contains("net46content/ext.txt", net46files);
                            Assert.Contains("net46content/cs/ext.cs.txt", net46files);
                        }
                        else
                        {
                            Assert.Equal(0, net46files.Count());
                        }
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
        public void PackCommand_BuildOutputInnerTargetExtension_AddsTfmSpecificBuildOuput(string tfmProperty,
    string tfmValue)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                File.WriteAllText(Path.Combine(workingDirectory, "abc.dll"), "hello world");
                File.WriteAllText(Path.Combine(workingDirectory, "abc.pdb"), "hello world");
                var pathToDll = Path.Combine(workingDirectory, "abc.dll");
                var pathToPdb = Path.Combine(workingDirectory, "abc.pdb");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var target =
                        $@"<Target Name=""CustomBuildOutputTarget"">
    <ItemGroup>
      <BuildOutputInPackage Include=""abc.dll"">
        <FinalOutputPath>{pathToDll}</FinalOutputPath>
      </BuildOutputInPackage>
      <BuildOutputInPackage Include=""abc.pdb"">
        <FinalOutputPath>{pathToPdb}</FinalOutputPath>
      </BuildOutputInPackage>
    </ItemGroup>
  </Target>";
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddProperty(xml, "TargetsForTfmSpecificBuildOutput", "CustomBuildOutputTarget");
                    ProjectFileUtils.AddCustomXmlToProjectRoot(xml, target);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} /p:IncludeSymbols=true /p:SymbolPackageFormat=symbols.nupkg");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                var symbolNupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.symbols.nupkg");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                using (var symbolNupkgReader = new PackageArchiveReader(symbolNupkgPath))
                {
                    var tfms = tfmValue.Split(';');
                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    var symbolLibItems = symbolNupkgReader.GetLibItems().ToList();
                    Assert.Equal(tfms.Length, libItems.Count);

                    if (tfms.Length == 2)
                    {
                        Assert.Equal(FrameworkConstants.CommonFrameworks.Net46, libItems[0].TargetFramework);
                        Assert.Equal(new[] { "lib/net46/abc.dll", "lib/net46/ClassLibrary1.dll" },
                            libItems[0].Items);
                        Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[1].TargetFramework);
                        Assert.Equal(new[] { "lib/netstandard1.4/abc.dll", "lib/netstandard1.4/ClassLibrary1.dll" },
                            libItems[1].Items);
                        Assert.Equal(FrameworkConstants.CommonFrameworks.Net46, symbolLibItems[0].TargetFramework);
                        Assert.Equal(new[] { "lib/net46/abc.dll", "lib/net46/abc.pdb", "lib/net46/ClassLibrary1.dll", "lib/net46/ClassLibrary1.pdb" },
                            symbolLibItems[0].Items);
                        Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, symbolLibItems[1].TargetFramework);
                        Assert.Equal(new[] { "lib/netstandard1.4/abc.dll", "lib/netstandard1.4/abc.pdb", "lib/netstandard1.4/ClassLibrary1.dll", "lib/netstandard1.4/ClassLibrary1.pdb" },
                            symbolLibItems[1].Items);
                    }
                    else
                    {
                        Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                        Assert.Equal(new[] { "lib/netstandard1.4/abc.dll", "lib/netstandard1.4/ClassLibrary1.dll" },
                            libItems[0].Items);
                        Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, symbolLibItems[0].TargetFramework);
                        Assert.Equal(new[] { "lib/netstandard1.4/abc.dll", "lib/netstandard1.4/abc.pdb", "lib/netstandard1.4/ClassLibrary1.dll", "lib/netstandard1.4/ClassLibrary1.pdb" },
                            symbolLibItems[0].Items);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("folderA\\**\\*", null, "content/folderA/folderA.txt;content/folderA/folderB/folderB.txt;" +
                                                                                                "contentFiles/any/netstandard1.4/folderA/folderA.txt;" +
                                                                                                "contentFiles/any/netstandard1.4/folderA/folderB/folderB.txt")]
        [InlineData("folderA\\**\\*", "pkgA", "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        [InlineData("folderA\\**\\*", "pkgA/", "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        [InlineData("folderA\\**\\*", "pkgA\\", "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        [InlineData("folderA\\**", "pkgA", "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        public void PackCommand_PackProject_GlobbingPathsPacksContentCorrectly(string sourcePath, string packagePath,
            string expectedTargetPaths)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    if (packagePath != null)
                    {
                        properties["PackagePath"] = packagePath;
                    }
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                File.WriteAllText(Path.Combine(workingDirectory, "folderA", "folderA.txt"), "hello world from subfolder A directory");
                File.WriteAllText(Path.Combine(workingDirectory, "folderA", "folderB", "folderB.txt"), "hello world from subfolder B directory");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var items = new HashSet<string>(nupkgReader.GetFiles());
                    var expectedPaths = expectedTargetPaths.Split(';');
                    // we add 5 because of the 5 standard files present in the nupkg that won't change.
                    Assert.Equal(items.Count(), expectedPaths.Length + 5);
                    foreach (var path in expectedPaths)
                    {
                        Assert.Contains(path, items);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]

        [InlineData("PresentationFramework", true, "netstandard1.4;net461", "", "net461")]
        [InlineData("PresentationFramework", false, "netstandard1.4;net461", "", "net461")]
        [InlineData("System.IO", true, "netstandard1.4;net46", "", "net46")]
        [InlineData("System.IO", true, "net46;net461", "net461", "net461")]
        [InlineData("System.IO", true, "net461", "", "net461")]
        public void PackCommand_PackProject_AddsReferenceAsFrameworkAssemblyReference(string referenceAssembly, bool pack,
            string targetFrameworks, string conditionalFramework, string expectedTargetFramework)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                // Create the subdirectory structure for testing possible source paths for the content file
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var frameworkProperty = "TargetFrameworks";
                    if (targetFrameworks.Split(';').Count() == 1)
                    {
                        frameworkProperty = "TargetFramework";
                    }
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, frameworkProperty, targetFrameworks);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    if (!pack)
                    {
                        attributes["Pack"] = "false";
                    }
                    ProjectFileUtils.AddItem(
                        xml,
                        "Reference",
                        referenceAssembly,
                        conditionalFramework,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                //XPlatTestUtils.WaitForDebugger();
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var expectedFrameworks = expectedTargetFramework.Split(';');
                    var frameworkItems = nupkgReader.GetFrameworkItems();
                    foreach (var framework in expectedFrameworks)
                    {
                        var nugetFramework = NuGetFramework.Parse(framework);
                        var frameworkSpecificGroup = frameworkItems.Where(t => t.TargetFramework.Equals(nugetFramework)).FirstOrDefault();
                        if (pack)
                        {
                            Assert.True(frameworkSpecificGroup?.Items.Contains(referenceAssembly));
                        }
                        else
                        {
                            Assert.Null(frameworkSpecificGroup);
                        }

                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("Content", "", "Content")]
        [InlineData("Content", "Page", "Page")]
        [InlineData("EmbeddedResource", "", "EmbeddedResource")]
        [InlineData("EmbeddedResource", "ApplicationDefinition", "ApplicationDefinition")]
        [InlineData("Content", "LinkDescription", "LinkDescription")]
        [InlineData("Content", "RandomBuildAction", "RandomBuildAction")]
        public void PackCommand_PackProject_OutputsBuildActionForContentFiles(string itemType, string buildAction, string expectedBuildAction)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                // Create the subdirectory structure for testing possible source paths for the content file
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                File.WriteAllBytes(Path.Combine(workingDirectory, "abc.png"), new byte[0]);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>();
                    attributes["Pack"] = "true";
                    var properties = new Dictionary<string, string>();
                    properties["BuildAction"] = buildAction;

                    ProjectFileUtils.AddItem(
                        xml,
                        itemType,
                        "abc.png",
                        NuGetFramework.AnyFramework,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var contentFiles = nuspecReader.GetContentFiles().ToArray();

                    Assert.True(contentFiles.Count() == 1);
                    var contentFile = contentFiles[0];
                    Assert.Equal(expectedBuildAction, contentFile.BuildAction);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackSolution_AddsProjectRefsAsPackageRefs()
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var solutionName = "Solution1";
                var projectName = "ClassLibrary1";
                var referencedProject1 = "ClassLibrary2";
                var referencedProject2 = "ClassLibrary3";

                var projectAndReference1Folder = "Src";
                var rederence2Folder = "src";

                var projectFolder = Path.Combine(testDirectory.Path, projectAndReference1Folder, projectName);

                var projectFileRelativ = Path.Combine(projectAndReference1Folder, projectName, $"{projectName}.csproj");
                var referencedProject1RelativDir = Path.Combine(projectAndReference1Folder, referencedProject1, $"{referencedProject1}.csproj");
                var referencedProject2RelativDir = Path.Combine(rederence2Folder, referencedProject2, $"{referencedProject2}.csproj");

                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, projectAndReference1Folder), projectName, "classlib -f netstandard2.0");
                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, projectAndReference1Folder), referencedProject1, "classlib -f netstandard2.0");
                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, rederence2Folder), referencedProject2, "classlib -f netstandard2.0");

                msbuildFixture.RunDotnet(testDirectory.Path, $"new solution -n {solutionName}");
                msbuildFixture.RunDotnet(testDirectory.Path, $"sln {solutionName}.sln add {projectFileRelativ}");
                msbuildFixture.RunDotnet(testDirectory.Path, $"sln {solutionName}.sln add {referencedProject1RelativDir}");
                msbuildFixture.RunDotnet(testDirectory.Path, $"sln {solutionName}.sln add {referencedProject2RelativDir}");

                var projectFile = Path.Combine(testDirectory.Path, projectFileRelativ);
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        @"..\ClassLibrary2\ClassLibrary2.csproj",
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        @"..\ClassLibrary3\ClassLibrary3.csproj",
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreSolution(testDirectory, solutionName, string.Empty);

                // Act
                msbuildFixture.PackSolution(testDirectory, solutionName, $"-o {testDirectory}");

                var nupkgPath = Path.Combine(testDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(projectFolder, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                Assert.Equal("Src", projectAndReference1Folder);
                Assert.Equal("src", rederence2Folder);

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(1,
                        dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, dependencyGroups[0].TargetFramework);
                    var packagesA = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(2, packagesA.Count);
                    Assert.Equal(referencedProject1, packagesA[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.0.0")), packagesA[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    Assert.Equal(referencedProject2, packagesA[1].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.0.0")), packagesA[1].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesA[1].Exclude);
                    Assert.Empty(packagesA[1].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(
                        new[]
                        {$"lib/netstandard2.0/{projectName}.dll"},
                        libItems[0].Items);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
#if NETCOREAPP5_0
        [InlineData("TargetFramework", "net5.0")]
        [InlineData("TargetFrameworks", "netstandard1.4;net5.0")]
#endif
        public void PackCommand_PackTargetHook_ExecutesBeforePack(string tfmProperty,
    string tfmValue)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var target =
                        $@"<Target Name=""RunBeforePack"">
    <Message Text= ""Hello World"" Importance=""High""/>
    </Target>";
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddProperty(xml, "BeforePack", "RunBeforePack");
                    ProjectFileUtils.AddCustomXmlToProjectRoot(xml, target);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");
                var indexOfHelloWorld = result.AllOutput.IndexOf("Hello World");
                var indexOfPackSuccessful = result.AllOutput.IndexOf("Successfully created package");
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                Assert.True(indexOfHelloWorld < indexOfPackSuccessful, "The custom target RunBeforePack did not run before pack target.");

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
#if NETCOREAPP5_0
        [InlineData("TargetFramework", "net5.0")]
        [InlineData("TargetFrameworks", "netstandard1.4;net5.0")]
#endif
        public void PackCommand_PackTarget_IsIncremental(string tfmProperty, string tfmValue)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} -bl:firstPack.binlog");

                var nupkgLastWriteTime = File.GetLastWriteTimeUtc(nupkgPath);
                var nuspecLastWriteTime = File.GetLastWriteTimeUtc(nuspecPath);

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} -bl:secondPack.binlog");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                Assert.Equal(nupkgLastWriteTime, File.GetLastWriteTimeUtc(nupkgPath));
                Assert.Equal(nuspecLastWriteTime, File.GetLastWriteTimeUtc(nuspecPath));
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("NoWarn", "NU5125", false)]
        [InlineData("NoWarn", "NU5106", true)]
        public void PackCommand_NoWarn_SuppressesWarnings(string property, string value, bool expectToWarn)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.AddProperty(xml, property, value);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseUrl", "http://contoso.com/license.html");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                var expectedWarning = string.Format("warning " + NuGetLogCode.NU5125 + ": " + NuGet.Packaging.Rules.AnalysisResources.LicenseUrlDeprecationWarning);

                if (expectToWarn)
                {
                    result.AllOutput.Should().Contain(expectedWarning);
                }
                else
                {
                    result.AllOutput.Should().NotContain(expectedWarning);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("WarningsAsErrors", "NU5102", true)]
        [InlineData("WarningsAsErrors", "NU5106", false)]
        [InlineData("TreatWarningsAsErrors", "true", true)]
        public void PackCommand_WarnAsError_PrintsWarningsAsErrors(string property, string value, bool expectToError)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var semver2Version = "1.0.0-rtm+asdassd";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.AddProperty(xml, "PackageProjectUrl", "http://project_url_here_or_delete_this_line/");
                    ProjectFileUtils.AddProperty(xml, property, value);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0-rtm.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0-rtm.nuspec");

                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} /p:Version={semver2Version}", validateSuccess: false);

                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                if (expectToError)
                {
                    result.AllOutput.Should().Contain(NuGetLogCode.NU5102.ToString());
                    result.ExitCode.Should().NotBe(0);
                    result.AllOutput.Should().NotContain("success");
                    Assert.False(File.Exists(nupkgPath), "The output .nupkg should not exist when pack fails.");
                }
                else
                {
                    Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_IncrementalPack_FailsWhenInvokedTwiceInARow()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var semver2Version = "1.0.0-rtm+asdassd";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.AddProperty(xml, "PackageProjectUrl", "http://project_url_here_or_delete_this_line/");
                    ProjectFileUtils.AddProperty(xml, "TreatWarningsAsErrors", "true");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0-rtm.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0-rtm.nuspec");

                // Call once.
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} /p:Version={semver2Version}", validateSuccess: false);
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                Assert.False(File.Exists(nupkgPath), "The output .nupkg should not exist when pack fails.");
                result.AllOutput.Should().Contain(NuGetLogCode.NU5102.ToString());
                result.ExitCode.Should().NotBe(0);
                result.AllOutput.Should().NotContain("success");

                // Call twice.
                result = msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} /p:Version={semver2Version}", validateSuccess: false);
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                Assert.False(File.Exists(nupkgPath), "The output .nupkg should not exist when pack fails.");
                result.AllOutput.Should().Contain(NuGetLogCode.NU5102.ToString());
                result.ExitCode.Should().NotBe(0);
                result.AllOutput.Should().NotContain("success");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackWithRepositoryVerifyNuspec()
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
                    ProjectFileUtils.AddProperty(xml, "RepositoryType", "git");
                    ProjectFileUtils.AddProperty(xml, "RepositoryUrl", "https://github.com/NuGet/NuGet.Client.git");
                    ProjectFileUtils.AddProperty(xml, "RepositoryBranch", "dev");
                    ProjectFileUtils.AddProperty(xml, "RepositoryCommit", "e1c65e4524cd70ee6e22abe33e6cb6ec73938cb3");
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
                    nuspecReader.GetRepositoryMetadata().Type.Should().Be("git");
                    nuspecReader.GetRepositoryMetadata().Url.Should().Be("https://github.com/NuGet/NuGet.Client.git");
                    nuspecReader.GetRepositoryMetadata().Branch.Should().Be("dev");
                    nuspecReader.GetRepositoryMetadata().Commit.Should().Be("e1c65e4524cd70ee6e22abe33e6cb6ec73938cb3");
                }
            }
        }



        [PlatformFact(Platform.Windows)]
        public void PackCommand_ManualAddPackage_DevelopmentDependency()
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net45");

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "1.0.2";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "StyleCop.Analyzers",
                        "net45",
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(1,
                        dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[0].TargetFramework);
                    Assert.Equal(1, dependencyGroups[0].Packages.Count());
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("net45", "netstandard1.3")]
        [InlineData("netstandard1.3", "net45")]
        [InlineData("", "")]
        public void PackCommand_SuppressDependencies_DoesNotContainAnyDependency(string frameworkToSuppress, string expectedInFramework)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "net45;netstandard1.3");
                    if (!string.IsNullOrEmpty(frameworkToSuppress))
                    {
                        ProjectFileUtils.AddProperty(xml, "SuppressDependenciesWhenPacking", "true", $"'$(TargetFramework)'=='{frameworkToSuppress}'");
                    }
                    else
                    {
                        ProjectFileUtils.AddProperty(xml, "SuppressDependenciesWhenPacking", "true");
                    }

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "9.0.1";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.json",
                        "",
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var expectedFrameworks = expectedInFramework.Split(';');
                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(expectedFrameworks.Where(t => !string.IsNullOrEmpty(t)).Count(),
                        dependencyGroups.Count);

                    if (dependencyGroups.Count > 0)
                    {
                        Assert.Equal(dependencyGroups[0].TargetFramework, NuGetFramework.Parse(expectedFrameworks[0]));
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackEmbedInteropPackage()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup BuildOutputTargetFolder
                var buildTargetFolders = "lib;embed";
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "BuildOutputTargetFolder", buildTargetFolders);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // Act
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    // Validate Compile assets
                    foreach (var buildTargetFolder in buildTargetFolders.Split(';'))
                    {
                        var compileItems = nupkgReader.GetFiles(buildTargetFolder).ToList();
                        Assert.Equal(1, compileItems.Count);
                        Assert.Equal(buildTargetFolder + "/netstandard2.0/ClassLibrary1.dll", compileItems[0]);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("Microsoft.NETCore.App", "true", "netcoreapp3.0", "", "netcoreapp3.0")]
        [InlineData("Microsoft.NETCore.App", "false", "netcoreapp3.0", "", "netcoreapp3.0")]
        [InlineData("Microsoft.WindowsDesktop.App", "true", "netstandard2.1;netcoreapp3.0", "netcoreapp3.0", "netcoreapp3.0")]
        [InlineData("Microsoft.WindowsDesktop.App;Microsoft.AspNetCore.App", "true;true", "netcoreapp3.0", "netcoreapp3.0", "netcoreapp3.0")]
        [InlineData("Microsoft.WindowsDesktop.App.WPF;Microsoft.WindowsDesktop.App.WindowsForms", "true;false", "netcoreapp3.0", "", "netcoreapp3.0")]
        public void PackCommand_PackProject_PacksFrameworkReferences(string frameworkReferences, string packForFrameworkRefs, string targetFrameworks, string conditionalFramework, string expectedTargetFramework)
        {
            // Arrange
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                var frameworkReftoPack = new Dictionary<string, bool>();
                var frameworkRefs = frameworkReferences.Split(";");
                var pack = packForFrameworkRefs.Split(";").Select(e => bool.Parse(e)).ToArray();
                Assert.Equal(frameworkRefs.Length, pack.Length);
                for (var i = 0; i < frameworkRefs.Length; i++)
                {
                    frameworkReftoPack.Add(frameworkRefs[i], pack[i]);
                }

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var frameworkProperty = "TargetFrameworks";
                    if (targetFrameworks.Split(';').Count() == 1)
                    {
                        frameworkProperty = "TargetFramework";
                    }
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, frameworkProperty, targetFrameworks);

                    foreach (var frameworkRef in frameworkReftoPack)
                    {
                        var attributes = new Dictionary<string, string>();

                        var properties = new Dictionary<string, string>();
                        if (!frameworkRef.Value)
                        {
                            attributes["PrivateAssets"] = "all";
                        }
                        ProjectFileUtils.AddItem(
                            xml,
                            "FrameworkReference",
                            frameworkRef.Key,
                            conditionalFramework,
                            properties,
                            attributes);
                    }


                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var expectedFrameworks = expectedTargetFramework.Split(';');

                    var frameworkItems = nupkgReader.NuspecReader.GetFrameworkRefGroups();
                    foreach (var framework in expectedFrameworks)
                    {
                        var nugetFramework = NuGetFramework.Parse(framework);
                        var frameworkSpecificGroup = frameworkItems.Where(t => t.TargetFramework.Equals(nugetFramework)).FirstOrDefault();

                        foreach (var frameworkRef in frameworkReftoPack)
                        {
                            if (frameworkRef.Value)
                            {
                                Assert.True(frameworkSpecificGroup?.FrameworkReferences.Contains(new FrameworkReference(frameworkRef.Key)));
                            }
                            else
                            {
                                Assert.False(frameworkSpecificGroup == null ? false : frameworkSpecificGroup.FrameworkReferences.Select(e => e.Name).Contains(frameworkRef.Key));
                            }
                        }
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_WithGeneratePackageOnBuildSet_CanPublish()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " console");

                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }
                // Act
                var result = msbuildFixture.RunDotnet(workingDirectory, $"publish {projectFile}");

                // Assert
                Assert.True(result.Success);
            }
        }

        [PlatformFact(Platform.Windows, Skip = "https://github.com/NuGet/Home/issues/8601")]
        public void PackCommand_Deterministic_MultiplePackInvocations_CreateIdenticalPackages()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "Deterministic", "true");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                // Act
                byte[][] packageBytes = new byte[2][];

                for (var i = 0; i < 2; i++)
                {
                    var packageOutputPath = Path.Combine(workingDirectory, i.ToString());
                    var nupkgPath = Path.Combine(packageOutputPath, $"{projectName}.1.0.0.nupkg");
                    var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                    // Act
                    msbuildFixture.PackProject(workingDirectory, projectName, $"-o {packageOutputPath}");

                    Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                    Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                    using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                    {
                        var nuspecReader = nupkgReader.NuspecReader;

                        // Validate the output .nuspec.
                        Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                        Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    }

                    using (var reader = new FileStream(nupkgPath, FileMode.Open))
                    using (var ms = new MemoryStream())
                    {
                        reader.CopyTo(ms);
                        packageBytes[i] = ms.ToArray();
                    }
                }
                // Assert
                Assert.Equal(packageBytes[0], packageBytes[1]);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackIcon_WithNuspec_IconUrl_Warns_Succeeds()
        {
            var testDirBuilder = TestDirectoryBuilder.Create();
            var projectBuilder = ProjectFileBuilder.Create();
            var nuspecBuilder = NuspecBuilder.Create();

            nuspecBuilder
                .WithIconUrl("https://test/icon2.jpg")
                .WithFile("dummy.txt");

            projectBuilder
                .WithProjectName("test")
                .WithProperty("Authors", "Alice")
                .WithProperty("NuspecFile", "test.nuspec")
                .WithPackageIconUrl("https://test/icon.jpg");

            testDirBuilder
                .WithNuspec(nuspecBuilder, "test\\test.nuspec")
                .WithFile("test\\dummy.txt", 10);

            using (var srcDir = msbuildFixture.Build(testDirBuilder))
            {
                projectBuilder.Build(msbuildFixture, srcDir.Path);
                var result = msbuildFixture.PackProject(projectBuilder.ProjectFolder, projectBuilder.ProjectName, string.Empty);

                Assert.Contains(NuGetLogCode.NU5048.ToString(), result.Output);
                Assert.Contains("iconUrl", result.Output);
                Assert.Contains("PackageIconUrl", result.Output);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_WhenUsingSemver2Version_NU5105_IsNotRaised()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.AddProperty(xml, "PackageVersion", "1.0.0+mySpecialSemver2Metadata");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                Assert.True(File.Exists(nupkgPath), $"The output .nupkg is not in the expected place. {result.AllOutput}");
                Assert.True(File.Exists(nuspecPath), $"The intermediate nuspec file is not in the expected place. {result.AllOutput}");
                result.AllOutput.Should().NotContain(NuGetLogCode.NU5105.ToString());
            }
        }

        [PlatformFact(Platform.Windows, Skip = "https://github.com/NuGet/Home/issues/10133")]
        public void PackCommand_PackProjectWithCentralTransitiveDependencies()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0", 60000);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Moq",
                        string.Empty,
                        new Dictionary<string, string>(),
                        new Dictionary<string, string>());

                    ProjectFileUtils.AddProperty(
                        xml,
                        "ManagePackageVersionsCentrally",
                        "true");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                // The test depends on the presence of these packages and their versions.
                // Change to Directory.Packages.props when new cli that supports NuGet.props will be downloaded
                var directoryPackagesPropsName = Path.Combine(workingDirectory, $"Directory.Build.props");
                var directoryPackagesPropsContent = @"<Project>
                        <ItemGroup>
                            <PackageVersion Include = ""Moq"" Version = ""4.10.0""/>
                            <PackageVersion Include = ""Castle.Core"" Version = ""4.4.0""/>
                        </ItemGroup>
                        <PropertyGroup>
	                        <CentralPackageVersionsFileImported>true</CentralPackageVersionsFileImported>
                        </PropertyGroup>
                    </Project>";
                File.WriteAllText(directoryPackagesPropsName, directoryPackagesPropsContent);

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
                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(2, packages.Count);
                    var moqPackage = packages.Where(p => p.Id.Equals("Moq", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    var castleCorePackage = packages.Where(p => p.Id.Equals("Castle.Core", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    Assert.NotNull(moqPackage);
                    Assert.NotNull(castleCorePackage);
                    Assert.Equal("[4.10.0, )", moqPackage.VersionRange.ToNormalizedString());
                    Assert.Equal("[4.4.0, )", castleCorePackage.VersionRange.ToNormalizedString());
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_DoesNotGenerateOwnersElement()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    ProjectFileUtils.AddProperty(xml, "Authors", "Some authors");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                var document = XDocument.Load(nuspecPath);
                var ns = document.Root.GetDefaultNamespace();

                Assert.Null(document.Root.Element(ns + "metadata").Element(ns + "owners"));
            }
        }
    }
}
