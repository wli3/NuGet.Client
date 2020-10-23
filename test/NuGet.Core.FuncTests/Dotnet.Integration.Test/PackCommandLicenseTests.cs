// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Licenses;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    public class PackCommandLicenseTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackCommandLicenseTests(MsbuildIntegrationTestFixture fixture)
        {
            msbuildFixture = fixture;
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("MIT")]
        [InlineData("MIT OR Apache-2.0 WITH 389-exception")]
        public void PackCommand_PackLicense_SimpleExpression_StandardLicense(string licenseExpr)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpression", licenseExpr);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                Assert.True(!result.AllOutput.Contains("NU5034"));

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());
                    Assert.Equal(new Uri(string.Format(LicenseMetadata.LicenseServiceLinkTemplate, licenseExpr)), new Uri(nuspecReader.GetLicenseUrl()));
                    var licenseMetadata = nuspecReader.GetLicenseMetadata();
                    Assert.NotNull(licenseMetadata);
                    Assert.Equal(licenseMetadata.LicenseUrl.OriginalString, nuspecReader.GetLicenseUrl());
                    Assert.Equal(licenseMetadata.LicenseUrl, new Uri(nuspecReader.GetLicenseUrl()));
                    Assert.Equal(licenseMetadata.Type, LicenseType.Expression);
                    Assert.Equal(licenseMetadata.Version, LicenseMetadata.EmptyVersion);
                    Assert.Equal(licenseMetadata.License, licenseExpr);
                    Assert.Equal(licenseExpr, licenseMetadata.LicenseExpression.ToString());
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_ComplexExpression_WithNonStandardLicense()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var customLicense = "LicenseRef-Nikolche";
                var licenseExpr = $"MIT OR {customLicense} WITH 389-exception";
                // Set up
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpression", licenseExpr);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                Assert.True(result.Success);
                Assert.True(result.AllOutput.Contains("NU5124"));

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());
                    Assert.Equal(new Uri(string.Format(LicenseMetadata.LicenseServiceLinkTemplate, licenseExpr)), new Uri(nuspecReader.GetLicenseUrl()));
                    var licenseMetadata = nuspecReader.GetLicenseMetadata();
                    Assert.NotNull(licenseMetadata);
                    Assert.Equal(licenseMetadata.LicenseUrl.OriginalString, nuspecReader.GetLicenseUrl());
                    Assert.Equal(licenseMetadata.LicenseUrl, new Uri(nuspecReader.GetLicenseUrl()));
                    Assert.Equal(licenseMetadata.Type, LicenseType.Expression);
                    Assert.Equal(licenseMetadata.Version, LicenseMetadata.EmptyVersion);
                    Assert.Equal(licenseMetadata.License, licenseExpr);
                    Assert.False(licenseMetadata.LicenseExpression.HasOnlyStandardIdentifiers());
                    Assert.Equal(licenseExpr, licenseMetadata.LicenseExpression.ToString());
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("Cant Parse This")]
        [InlineData("Tanana AND nana nana")]
        public void PackCommand_PackLicense_NonParsableExpressionFailsErrorWithCode(string licenseExpr)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpression", licenseExpr);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.False(File.Exists(nupkgPath));
                Assert.False(File.Exists(nuspecPath));

                Assert.False(result.Success);
                Assert.True(result.AllOutput.Contains("NU5032"));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_NonParsableVersionFailsErrorWithCode()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var licenseExpr = "MIT OR Apache-2.0";
                var version = "1.0.0-babanana";
                // Set up
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpression", licenseExpr);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpressionVersion", version);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.False(File.Exists(nupkgPath));
                Assert.False(File.Exists(nuspecPath));

                Assert.False(result.Success);
                Assert.True(result.AllOutput.Contains("NU5034"));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_ExpressionVersionHigherFailsWithErrorCode()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var licenseExpr = "MIT OR Apache-2.0";
                var version = "2.0.0";
                // Set up
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpression", licenseExpr);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpressionVersion", version);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.False(File.Exists(nupkgPath));
                Assert.False(File.Exists(nuspecPath));

                Assert.False(result.Success);
                Assert.True(result.AllOutput.Contains("NU5034"));
                Assert.True(result.AllOutput.Contains($"'{LicenseMetadata.CurrentVersion.ToString()}'"));
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(@"LICENSE", ".")]
        [InlineData("LICENSE.md", ".")]
        [InlineData("LICENSE.txt", "LICENSE.txt")]
        public void PackCommand_PackLicense_PackBasicLicenseFile(string licenseFileName, string packagesPath)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var licenseFile = Path.Combine(workingDirectory, licenseFileName);

                var licenseText = "Random licenseFile";
                File.WriteAllText(licenseFile, licenseText);
                Assert.True(File.Exists(licenseFile));

                File.WriteAllText(Path.Combine(Path.GetDirectoryName(projectFile), licenseFileName), "The best license ever.");

                // Setup LicenseFile
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseFile", licenseFileName);

                    var attributes = new Dictionary<string, string>();
                    attributes["Pack"] = "true";
                    attributes["PackagePath"] = packagesPath;
                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "None",
                        licenseFileName,
                        NuGetFramework.AnyFramework,
                        properties,
                        attributes);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                Assert.True(result.Success);

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());
                    Assert.Equal(LicenseMetadata.LicenseFileDeprecationUrl, new Uri(nuspecReader.GetLicenseUrl()));
                    var licenseMetadata = nuspecReader.GetLicenseMetadata();
                    Assert.NotNull(licenseMetadata);
                    Assert.Equal(LicenseMetadata.LicenseFileDeprecationUrl, licenseMetadata.LicenseUrl);
                    Assert.Equal(licenseMetadata.Type, LicenseType.File);
                    Assert.Equal(licenseMetadata.Version, LicenseMetadata.EmptyVersion);
                    Assert.Equal(licenseMetadata.License, licenseFileName);
                    Assert.Null(licenseMetadata.LicenseExpression);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_PackBasicLicenseFile_FileNotInPackage()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var licenseFileName = "LICENSE.txt";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var licenseFile = Path.Combine(workingDirectory, licenseFileName);

                var licenseText = "Random licenseFile";
                File.WriteAllText(licenseFile, licenseText);
                Assert.True(File.Exists(licenseFile));

                // Setup LicenseFile
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseFile", licenseFileName);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.False(File.Exists(nupkgPath));
                Assert.True(File.Exists(nuspecPath)); // See https://github.com/NuGet/Home/issues/7348. This needs to be fixed.
                Assert.False(result.Success);
                Assert.True(result.Output.Contains("NU5030"));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_PackBasicLicenseFile_FileExtensionNotValid()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                var licenseFileName = "LICENSE.badextension";
                // Set up
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var licenseFile = Path.Combine(workingDirectory, licenseFileName);

                var licenseText = "Random licenseFile";
                File.WriteAllText(licenseFile, licenseText);
                Assert.True(File.Exists(licenseFile));

                // Setup LicenseFile
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseFile", licenseFileName);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.False(File.Exists(nupkgPath));
                Assert.True(File.Exists(nuspecPath)); // See https://github.com/NuGet/Home/issues/7348. This needs to be fixed.
                Assert.False(result.Success);
                Assert.True(result.Output.Contains("NU5031"));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_BothLicenseExpressionAndFile_FailsWithErrorCode()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var licenseExpr = "MIT";
                var licenseFile = "LICENSE.txt";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseExpression", licenseExpr);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseFile", licenseFile);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.False(File.Exists(nupkgPath));
                Assert.False(File.Exists(nuspecPath));

                Assert.False(result.Success);
                Assert.True(result.AllOutput.Contains("NU5033"));
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_LicenseUrlIsBeingDeprecated()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var projectUrl = new Uri("https://www.coolproject.com/license.txt");
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseUrl", projectUrl.ToString());
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath));
                Assert.True(File.Exists(nuspecPath));

                Assert.True(result.AllOutput.Contains("NU5125"));

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());
                    Assert.Equal(projectUrl, new Uri(nuspecReader.GetLicenseUrl()));
                    var licenseMetadata = nuspecReader.GetLicenseMetadata();
                    Assert.Null(licenseMetadata);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_IncludeLicenseFileWithSnupkg()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var licenseFileName = "LICENSE.txt";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var licenseFile = Path.Combine(workingDirectory, licenseFileName);
                File.WriteAllText(licenseFile, "Random licenseFile");
                Assert.True(File.Exists(licenseFile));

                // Setup LicenseFile
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseFile", licenseFileName);

                    var attributes = new Dictionary<string, string>();
                    attributes["Pack"] = "true";
                    attributes["PackagePath"] = licenseFileName;
                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "None",
                        licenseFileName,
                        NuGetFramework.AnyFramework,
                        properties,
                        attributes);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");
                msbuildFixture.PackProject(workingDirectory, projectName, $"--include-symbols /p:SymbolPackageFormat=snupkg -o {workingDirectory}");


                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                var symbolPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.snupkg");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                Assert.True(File.Exists(symbolPath), "The output .snupkg is not in the expected place");

                Assert.True(result.Success);

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());
                    Assert.Equal(LicenseMetadata.LicenseFileDeprecationUrl, new Uri(nuspecReader.GetLicenseUrl()));
                    var licenseMetadata = nuspecReader.GetLicenseMetadata();
                    Assert.NotNull(licenseMetadata);
                    Assert.Equal(LicenseMetadata.LicenseFileDeprecationUrl, licenseMetadata.LicenseUrl);
                    Assert.Equal(licenseMetadata.Type, LicenseType.File);
                    Assert.Equal(licenseMetadata.Version, LicenseMetadata.EmptyVersion);
                    Assert.Equal(licenseMetadata.License, licenseFileName);
                    Assert.Null(licenseMetadata.LicenseExpression);
                }

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                using (var symbolReader = new PackageArchiveReader(symbolPath))
                {
                    // Validate the assets.
                    Assert.False(symbolReader.NuspecReader.GetRequireLicenseAcceptance());
                    Assert.Null(symbolReader.NuspecReader.GetLicenseMetadata());
                    Assert.Null(symbolReader.NuspecReader.GetLicenseUrl());
                    var libItems = nupkgReader.GetLibItems().ToList();
                    var libSymItems = symbolReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(1, libSymItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.dll" }, libItems[0].Items);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.pdb" }, libSymItems[0].Items);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackLicense_IncludeLicenseFileWithSymbolsNupkg()
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var licenseFileName = "LICENSE.txt";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib -f netstandard2.0");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                var licenseFile = Path.Combine(workingDirectory, licenseFileName);
                File.WriteAllText(licenseFile, "Random licenseFile");
                Assert.True(File.Exists(licenseFile));

                // Setup LicenseFile
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseFile", licenseFileName);

                    var attributes = new Dictionary<string, string>();
                    attributes["Pack"] = "true";
                    attributes["PackagePath"] = licenseFileName;
                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "None",
                        licenseFileName,
                        NuGetFramework.AnyFramework,
                        properties,
                        attributes);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");
                msbuildFixture.PackProject(workingDirectory, projectName, $"--include-symbols /p:SymbolPackageFormat=symbols.nupkg -o {workingDirectory}");


                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                var symbolPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.symbols.nupkg");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                Assert.True(File.Exists(symbolPath), "The output .snupkg is not in the expected place");

                Assert.True(result.Success);

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());
                    Assert.Equal(LicenseMetadata.LicenseFileDeprecationUrl, new Uri(nuspecReader.GetLicenseUrl()));
                    var licenseMetadata = nuspecReader.GetLicenseMetadata();
                    Assert.NotNull(licenseMetadata);
                    Assert.Equal(LicenseMetadata.LicenseFileDeprecationUrl, licenseMetadata.LicenseUrl);
                    Assert.Equal(licenseMetadata.Type, LicenseType.File);
                    Assert.Equal(licenseMetadata.Version, LicenseMetadata.EmptyVersion);
                    Assert.Equal(licenseMetadata.License, licenseFileName);
                    Assert.Null(licenseMetadata.LicenseExpression);
                }

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                using (var symbolReader = new PackageArchiveReader(symbolPath))
                {
                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    var libSymItems = symbolReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(1, libSymItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.dll" }, libItems[0].Items);
                    Assert.Equal(new[] { "lib/netstandard2.0/ClassLibrary1.dll", "lib/netstandard2.0/ClassLibrary1.pdb" }, libSymItems[0].Items);
                    Assert.True(symbolReader.GetEntry(symbolReader.NuspecReader.GetLicenseMetadata().License) != null);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("PackageLicenseExpression")]
        [InlineData("PackageLicenseFile")]
        public void PackCommand_PackLicense_LicenseExpressionAndLicenseUrlInConjunction(string licenseType)
        {
            using (var testDirectory = msbuildFixture.CreateTestDirectory())
            {
                // Set up
                var projectName = "ClassLibrary1";
                var licenseExpr = "MIT";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                // Setup LicenseExpression
                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, licenseType, licenseExpr);
                    ProjectFileUtils.AddProperty(xml, "PackageLicenseUrl", "https://www.mycoolproject.org/license.txt");
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var result = msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}", validateSuccess: false);

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.False(File.Exists(nupkgPath));
                Assert.False(File.Exists(nuspecPath));

                Assert.False(result.Success);
                Assert.True(result.AllOutput.Contains("NU5035"));
            }
        }

    }
}

