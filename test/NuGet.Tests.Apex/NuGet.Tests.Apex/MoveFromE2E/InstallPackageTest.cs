// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using NuGet.Test.Utility;
using Test.Utility.Signing;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Tests.Apex.MoveFromE2E
{
    public class InstallPackageTest : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public InstallPackageTest(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory, ITestOutputHelper output)
            : base(visualStudioHostFixtureFactory, output)
        {
        }

        // basic install into a build integrated project
        [StaFact]
        public void InstallPackageWithInvalidAbsoluteLocalSource()
        {
            // Arrange
            EnsureVisualStudioHost();

            using (var testContext = new ApexTestContext(VisualStudio, ProjectTemplate.ConsoleApplication, XunitLogger))
            {
                VisualStudio.AssertNoErrors();
                var packageName = "Rules";
                var source = "c:\\temp\\data";
                var message = "Unable to find package '$package' at source '$source'. Source not found.";

                var nugetConsole = GetConsole(testContext.Project);

                nugetConsole.InstallPackageFromPMC(packageName, source, false);

                nugetConsole.IsMessageFoundInPMC(message).Should().BeTrue("Install failed message shown.");
            }
        }

    }
}
