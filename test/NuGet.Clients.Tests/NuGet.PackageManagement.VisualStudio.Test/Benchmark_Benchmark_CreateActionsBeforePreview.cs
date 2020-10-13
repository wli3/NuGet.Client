// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.ProjectSystem;
using Moq;
using NuGet.Commands;
using NuGet.Commands.Test;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Test.Utility;
using Xunit;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class Benchmark_Benchmark_CreateActionsBeforePreview
    {
        [Params(4, 10, 20)]
        public int ProjectCount;
        TestDirectory TestDirectory;
        TestSolutionManager TestSolutionManager;

        [GlobalSetup]
        public void GlobalSetup()
        {
            TestDirectory = NuGet.Test.Utility.TestDirectory.Create();
            TestSolutionManager = new TestSolutionManager();
            
        }

        private (TestDirectory, TestSolutionManager) Setup()
        {
            TestDirectory TestDirectory = NuGet.Test.Utility.TestDirectory.Create();
            TestSolutionManager TestSolutionManager = new TestSolutionManager();
            return (TestDirectory, TestSolutionManager);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            TestDirectory?.Dispose();
            TestSolutionManager?.Dispose();
        }

        private void Cleanup(TestDirectory TestDirectory, TestSolutionManager TestSolutionManager)
        {
            TestDirectory?.Dispose();
            TestSolutionManager?.Dispose();
        }

        [Fact]
        [Benchmark(Baseline = true)]
        public async Task Benchmark_CreateActionsBeforePreview_Parallel()
        {
            // Set up Package Source
            var sources = new List<PackageSource>();
            var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
            var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
            var packageA = packageA_Version100.Identity;
            var packageB = packageB_Version100.Identity;
            var packageSource = Path.Combine(TestDirectory, "packageSource");
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                packageSource,
                PackageSaveMode.Defaultv3,
                packageA_Version100,
                packageB_Version100
                );

            sources.Add(new PackageSource(packageSource));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

            // Project
            int numberOfProjects = ProjectCount;
            var projectCache = new ProjectSystemCache();
            IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
            ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
            var packageSpecs = new PackageSpec[numberOfProjects];
            var projectFullPaths = new string[numberOfProjects];
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                NullSettings.Instance,
                TestSolutionManager,
                deleteOnRestartManager);

            var testNuGetProjectContext = new TestNuGetProjectContext();
            var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
            var prevProj = string.Empty;
            PackageSpec prevPackageSpec = null;

            // Create projects
            for (var i = numberOfProjects - 1; i >= 0; i--)
            {
                var projectName = $"project{i}";
                var projectFullPath = Path.Combine(TestDirectory.Path, projectName, projectName + ".csproj");
                var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache);

                // We need to treat NU1605 warning as error.
                project.IsNu1605Error = true;
                netCorePackageReferenceProjects.Add(project);
                TestSolutionManager.NuGetProjects.Add(project);

                //Let new project pickup my custom package source.
                project.ProjectLocalSources.AddRange(sources);
                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(
                    projectName,
                    projectFullPath,
                    packageA_Version100.Version);

                if (prevPackageSpec != null)
                {
                    packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                }

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                prevProj = projectFullPath;
                packageSpecs[i] = packageSpec;
                prevPackageSpec = packageSpec;
                projectFullPaths[i] = projectFullPath;
            }

            var initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();

            for (int i = 0; i < numberOfProjects; i++)
            {
                // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                var installed = new LibraryDependency
                {
                    LibraryRange = new LibraryRange(
                        name: packageB.Id,
                        versionRange: new VersionRange(packageB.Version),
                        typeConstraint: LibraryDependencyTarget.Package),
                };

                var packageSpec = packageSpecs[i];
                packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
            }

            // Act
            var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                netCorePackageReferenceProjects, // All projects
                packageB,
                resolutionContext,
                testNuGetProjectContext,
                sourceRepositoryProvider.GetRepositories().ToList(),
                CancellationToken.None);

            //var actions = results.Select(a => a.Action).ToArray();

            //await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
            //    netCorePackageReferenceProjects,
            //    actions,
            //    testNuGetProjectContext,
            //    new SourceCacheContext(),
            //    CancellationToken.None);

            //// Assert
            //Assert.Equal(initialInstalledPackages.Count(), 1);
            //var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
            //Assert.Equal(actions.Length, builtIntegratedActions.Count);
            //Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
            //Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
            //foreach (var netCorePackageReferenceProject in netCorePackageReferenceProjects)
            //{
            //    var finalInstalledPackages = (await netCorePackageReferenceProject.GetInstalledPackagesAsync(CancellationToken.None)).ToList();
            //    Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageA.Id
            //    && f.PackageIdentity.Version == packageA.Version));
            //    Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
            //    && f.PackageIdentity.Version == packageB.Version));
            //}

        }

        private TestNetCorePackageReferenceProject CreateTestNetCorePackageReferenceProject(string projectName, string projectFullPath, ProjectSystemCache projectSystemCache, TestProjectSystemServices projectServices = null)
        {
            projectServices = projectServices == null ? new TestProjectSystemServices() : projectServices;

            return new TestNetCorePackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectName,
                    projectFullPath: projectFullPath,
                    projectSystemCache: projectSystemCache,
                    unconfiguredProject: null,
                    projectServices: projectServices,
                    projectId: projectName);
        }

        private NetCorePackageReferenceProject CreateNetCorePackageReferenceProject(string projectName, string projectFullPath, ProjectSystemCache projectSystemCache)
        {
            var projectServices = new TestProjectSystemServices();

            return new NetCorePackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectName,
                    projectFullPath: projectFullPath,
                    projectSystemCache: projectSystemCache,
                    unconfiguredProject: null,
                    projectServices: projectServices,
                    projectId: projectName);
        }

        private ProjectNames GetTestProjectNames(string projectPath, string projectUniqueName)
        {
            var projectNames = new ProjectNames(
            fullName: projectPath,
            uniqueName: projectUniqueName,
            shortName: projectUniqueName,
            customUniqueName: projectUniqueName,
            projectId: Guid.NewGuid().ToString());
            return projectNames;
        }

        private static PackageSpec GetPackageSpec(string projectName, string testDirectory, string version)
        {
            string referenceSpec = $@"
                {{
                    ""frameworks"":
                    {{
                        ""net5.0"":
                        {{
                            ""dependencies"":
                            {{
                                ""packageA"":
                                {{
                                    ""version"": ""{version}"",
                                    ""target"": ""Package""
                                }},
                            }}
                        }}
                    }}
                }}";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private static PackageSpec GetPackageSpecNoPackages(string projectName, string testDirectory)
        {
            const string referenceSpec = @"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                }
                            }
                        }
                    }
                }";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private static PackageSpec GetPackageSpecMultipleVersions(string projectName, string testDirectory)
        {
            const string referenceSpec = @"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                    ""packageA"": {
                                    ""version"": ""[*, )"",
                                    ""target"": ""Package""
                                },
                                    ""packageB"": {
                                    ""version"": ""[1.0.0, )"",
                                    ""target"": ""Package""
                                },
                                    ""packageC"": {
                                    ""version"": ""[1.0.0, )"",
                                    ""target"": ""Package""
                                }
                            }
                        }
                    }
                }";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private class TestNetCorePackageReferenceProject
            : NetCorePackageReferenceProject
            , IProjectScriptHostService
            , IProjectSystemReferencesReader
        {
            public HashSet<PackageIdentity> ExecuteInitScriptAsyncCalls { get; }
                = new HashSet<PackageIdentity>(PackageIdentity.Comparer);

            public List<TestExternalProjectReference> ProjectReferences { get; }
                = new List<TestExternalProjectReference>();

            public bool IsCacheEnabled { get; set; }

            public bool IsNu1605Error { get; set; }

            public HashSet<PackageSource> ProjectLocalSources { get; set; } = new HashSet<PackageSource>();

            public TestNetCorePackageReferenceProject(
                string projectName,
                string projectUniqueName,
                string projectFullPath,
                IProjectSystemCache projectSystemCache,
                UnconfiguredProject unconfiguredProject,
                INuGetProjectServices projectServices,
                string projectId)
                : base(projectName, projectUniqueName, projectFullPath, projectSystemCache, unconfiguredProject, projectServices, projectId)
            {
                ProjectServices = projectServices;
            }

            public override string MSBuildProjectPath => base.MSBuildProjectPath;

            public override string ProjectName => base.ProjectName;

            public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
            {
                var packageSpecs = await base.GetPackageSpecsAsync(context);

                if (IsNu1605Error)
                {
                    foreach (var packageSpec in packageSpecs)
                    {
                        if (packageSpec?.RestoreMetadata != null)
                        {
                            var allWarningsAsErrors = false;
                            var noWarn = new HashSet<NuGetLogCode>();
                            var warnAsError = new HashSet<NuGetLogCode>();

                            if (packageSpec.RestoreMetadata.ProjectWideWarningProperties != null)
                            {
                                var warningProperties = packageSpec.RestoreMetadata.ProjectWideWarningProperties;
                                allWarningsAsErrors = warningProperties.AllWarningsAsErrors;
                                warnAsError.AddRange<NuGetLogCode>(warningProperties.WarningsAsErrors);
                                noWarn.AddRange<NuGetLogCode>(warningProperties.NoWarn);
                            }

                            warnAsError.Add(NuGetLogCode.NU1605);
                            noWarn.Remove(NuGetLogCode.NU1605);

                            packageSpec.RestoreMetadata.ProjectWideWarningProperties = new WarningProperties(warnAsError, noWarn, allWarningsAsErrors);

                            packageSpec?.RestoreMetadata.Sources.AddRange(new List<PackageSource>(ProjectLocalSources));
                        }
                    }
                }

                return packageSpecs;
            }

            public Task ExecutePackageScriptAsync(PackageIdentity packageIdentity, string packageInstallPath, string scriptRelativePath, INuGetProjectContext projectContext, bool throwOnFailure, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public Task<bool> ExecutePackageInitScriptAsync(PackageIdentity packageIdentity, string packageInstallPath, INuGetProjectContext projectContext, bool throwOnFailure, CancellationToken token)
            {
                ExecuteInitScriptAsyncCalls.Add(packageIdentity);
                return Task.FromResult(true);
            }

            public Task<IEnumerable<LibraryDependency>> GetPackageReferencesAsync(NuGetFramework targetFramework, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public Task<IEnumerable<ProjectRestoreReference>> GetProjectReferencesAsync(ILogger logger, CancellationToken token)
            {
                var projectRefs = ProjectReferences.Select(e => new ProjectRestoreReference()
                {
                    ProjectUniqueName = e.MSBuildProjectPath,
                    ProjectPath = e.MSBuildProjectPath,
                });

                return Task.FromResult(projectRefs);
            }

            public override Task PreProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                return base.PreProcessAsync(nuGetProjectContext, token);
            }

            public override Task PostProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                return base.PostProcessAsync(nuGetProjectContext, token);
            }

            public override Task<string> GetAssetsFilePathAsync()
            {
                return base.GetAssetsFilePathAsync();
            }

            public override Task<string> GetAssetsFilePathOrNullAsync()
            {
                return base.GetAssetsFilePathOrNullAsync();
            }

            public override Task AddFileToProjectAsync(string filePath)
            {
                return base.AddFileToProjectAsync(filePath);
            }

            public override Task<(IReadOnlyList<PackageSpec> dgSpecs, IReadOnlyList<IAssetsLogMessage> additionalMessages)> GetPackageSpecsAndAdditionalMessagesAsync(DependencyGraphCacheContext context)
            {
                return base.GetPackageSpecsAndAdditionalMessagesAsync(context);
            }

            public override async Task<bool> InstallPackageAsync(string packageId, VersionRange range, INuGetProjectContext nuGetProjectContext, BuildIntegratedInstallationContext installationContext, CancellationToken token)
            {
                var dependency = new LibraryDependency
                {
                    LibraryRange = new LibraryRange(
                        name: packageId,
                        versionRange: range,
                        typeConstraint: LibraryDependencyTarget.Package),
                    SuppressParent = installationContext.SuppressParent,
                    IncludeType = installationContext.IncludeType
                };

                await ProjectServices.References.AddOrUpdatePackageReferenceAsync(dependency, token);

                return true;
            }

            public override async Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                await ProjectServices.References.RemovePackageReferenceAsync(packageIdentity.Id);

                return true;
            }

            public override Task<string> GetCacheFilePathAsync()
            {
                return base.GetCacheFilePathAsync();
            }
        }

        private class TestExternalProjectReference
        {
            public IDependencyGraphProject Project { get; set; }

            public IDependencyGraphProject[] Children { get; set; }

            public TestExternalProjectReference(
                IDependencyGraphProject project,
                params IDependencyGraphProject[] children)
            {
                Project = project;
                Children = children;
                MSBuildProjectPath = project.MSBuildProjectPath;
            }

            public string MSBuildProjectPath { get; set; }
        }
    }
}
