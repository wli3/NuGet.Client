// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class PackCommandNewProjectOrSolutionTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackCommandNewProjectOrSolutionTests(MsbuildIntegrationTestFixture fixture)
        {
            msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_NewProject_OutputsInDefaultPaths()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var nupkgPath = Path.Combine(workingDirectory, @"bin\Debug", $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, @"obj\Debug", $"{projectName}.1.0.0.nuspec");

                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, "classlib");
                msbuildFixture.PackProject(workingDirectory, projectName, string.Empty, null);

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the default place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the default place");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_NewProject_ContinuousOutputInBothDefaultAndCustomPaths()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, "classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, string.Empty);

                // With default output path
                var nupkgPath = Path.Combine(workingDirectory, @"bin\Debug", $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, @"obj\Debug", $"{projectName}.1.0.0.nuspec");

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, "--no-build", null);

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the default place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the default place");

                // With custom output path
                var publishDir = Path.Combine(workingDirectory, "publish");
                nupkgPath = Path.Combine(publishDir, $"{projectName}.1.0.0.nupkg");
                nuspecPath = Path.Combine(publishDir, $"{projectName}.1.0.0.nuspec");

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"--no-build -o {publishDir}", publishDir);

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_NewSolution_OutputInDefaultPaths()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var solutionName = "Solution1";
                var projectName = "ClassLibrary1";
                var referencedProject1 = "ClassLibrary2";
                var referencedProject2 = "ClassLibrary3";

                var projectAndReference1Folder = "Src";
                var reference2Folder = "src";

                var projectFolder = Path.Combine(testDirectory.Path, projectAndReference1Folder, projectName);

                var projectFileRelativ = Path.Combine(projectAndReference1Folder, projectName, $"{projectName}.csproj");
                var referencedProject1RelativDir = Path.Combine(projectAndReference1Folder, referencedProject1, $"{referencedProject1}.csproj");
                var referencedProject2RelativDir = Path.Combine(reference2Folder, referencedProject2, $"{referencedProject2}.csproj");

                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, projectAndReference1Folder), projectName, "classlib");
                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, projectAndReference1Folder), referencedProject1, "classlib");
                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, reference2Folder), referencedProject2, "classlib");

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

                    ProjectFileUtils.AddItem(xml, "ProjectReference", @"..\ClassLibrary2\ClassLibrary2.csproj", string.Empty, properties, attributes);
                    ProjectFileUtils.AddItem(xml, "ProjectReference", @"..\ClassLibrary3\ClassLibrary3.csproj", string.Empty, properties, attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreSolution(testDirectory, solutionName, string.Empty);

                var nupkgPath = Path.Combine(projectFolder, @"bin\Debug", $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(projectFolder, @"obj\Debug", $"{projectName}.1.0.0.nuspec");

                // Act
                msbuildFixture.PackSolution(testDirectory, solutionName, string.Empty, null);

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the default place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the default place");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_NewSolution_ContinuousOutputInBothDefaultAndCustomPaths()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var solutionName = "Solution1";
                var projectName = "ClassLibrary1";
                var referencedProject1 = "ClassLibrary2";
                var referencedProject2 = "ClassLibrary3";

                var projectAndReference1Folder = "Src";
                var reference2Folder = "src";

                var projectFolder = Path.Combine(testDirectory.Path, projectAndReference1Folder, projectName);

                var projectFileRelativ = Path.Combine(projectAndReference1Folder, projectName, $"{projectName}.csproj");
                var referencedProject1RelativDir = Path.Combine(projectAndReference1Folder, referencedProject1, $"{referencedProject1}.csproj");
                var referencedProject2RelativDir = Path.Combine(reference2Folder, referencedProject2, $"{referencedProject2}.csproj");

                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, projectAndReference1Folder), projectName, "classlib");
                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, projectAndReference1Folder), referencedProject1, "classlib");
                msbuildFixture.CreateDotnetNewProject(Path.Combine(testDirectory.Path, reference2Folder), referencedProject2, "classlib");

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

                    ProjectFileUtils.AddItem(xml, "ProjectReference", @"..\ClassLibrary2\ClassLibrary2.csproj", string.Empty, properties, attributes);
                    ProjectFileUtils.AddItem(xml, "ProjectReference", @"..\ClassLibrary3\ClassLibrary3.csproj", string.Empty, properties, attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreSolution(testDirectory, solutionName, string.Empty);
                msbuildFixture.BuildSolution(testDirectory, solutionName, string.Empty);

                // With default output path within project folder

                // Arrange
                var nupkgPath = Path.Combine(projectFolder, @"bin\Debug", $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(projectFolder, @"obj\Debug", $"{projectName}.1.0.0.nuspec");

                // Act
                msbuildFixture.PackSolution(testDirectory, solutionName, "--no-build", null);

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the default place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the default place");

                // With common publish path within solution folder

                // Arrange
                var publishDir = Path.Combine(testDirectory.Path, "publish");
                nupkgPath = Path.Combine(publishDir, $"{projectName}.1.0.0.nupkg");
                nuspecPath = Path.Combine(publishDir, $"{projectName}.1.0.0.nuspec");

                msbuildFixture.PackSolution(testDirectory, solutionName, $"--no-build -o {publishDir}", publishDir);

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
            }
        }

        // This test checks to see that when GeneratePackageOnBuild is set to true, the generated nupkg and the intermediate
        // nuspec is deleted when the clean target is invoked.
        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewProject_CleanDeletesNupkgAndNuspec()
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
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");
                    ProjectFileUtils.AddProperty(xml, "NuspecOutputPath", "obj");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} ");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Run the clean target
                msbuildFixture.BuildProject(workingDirectory, projectName,
                    $"/t:Clean /p:PackageOutputPath={workingDirectory}\\");

                Assert.True(!File.Exists(nupkgPath), "The output .nupkg was not deleted by the Clean target");
                Assert.True(!File.Exists(nuspecPath), "The intermediate nuspec file was not deleted by the Clean target");
            }
        }

        // This test checks to see that when GeneratePackageOnBuild is set to true, the generated nupkg and the intermediate
        // nuspec is deleted when the clean target is invoked and no other nupkg or nuspec file in the PackageOutputPath.
        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewProject_CleanDeletesOnlyGeneratedNupkgAndNuspec()
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
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");
                    ProjectFileUtils.AddProperty(xml, "NuspecOutputPath", "obj");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} ");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                var extraNupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.2.0.nupkg");
                var extraNuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.2.0.nuspec");
                File.WriteAllBytes(extraNupkgPath, new byte[1024]);
                File.WriteAllText(extraNuspecPath, "hello world");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Run the clean target
                msbuildFixture.BuildProject(workingDirectory, projectName,
                    $"/t:Clean /p:PackageOutputPath={workingDirectory}\\");

                Assert.True(!File.Exists(nupkgPath), "The output .nupkg was not deleted by the Clean target");
                Assert.True(!File.Exists(nuspecPath), "The intermediate nuspec file was not deleted by the Clean target");
                Assert.True(File.Exists(extraNuspecPath), "All nuspec files were deleted by the clean target");
                Assert.True(File.Exists(extraNupkgPath), "All nupkg files were deleted by the clean target");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_NewProject_AddsTitleToNuspec()
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
                    ProjectFileUtils.AddProperty(xml, "Title", "MyPackageTitle");

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
                    var nuspecReader = nupkgReader.NuspecReader;
                    var intermediateNuspec = new NuspecReader(nuspecPath);

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("MyPackageTitle", nuspecReader.GetTitle());
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

                    // Validate title property in intermediate nuspec
                    Assert.Equal("MyPackageTitle", intermediateNuspec.GetTitle());
                }
            }
        }
    }
}
