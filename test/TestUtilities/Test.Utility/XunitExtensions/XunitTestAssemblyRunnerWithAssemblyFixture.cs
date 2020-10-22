// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NuGet.Test.Utility.XunitExtensions
{
    public class XunitTestAssemblyRunnerWithAssemblyFixture : XunitTestAssemblyRunner
    {
        readonly Dictionary<Type, object> assemblyFixtureMappings = new Dictionary<Type, object>();

        public XunitTestAssemblyRunnerWithAssemblyFixture(ITestAssembly testAssembly,
                                                          IEnumerable<IXunitTestCase> testCases,
                                                          IMessageSink diagnosticMessageSink,
                                                          IMessageSink executionMessageSink,
                                                          ITestFrameworkExecutionOptions executionOptions)
            : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
        { }

        protected override async Task AfterTestAssemblyStartingAsync()
        {
            // Let everything initialize
            await base.AfterTestAssemblyStartingAsync();

            // Go find all the AssemblyFixtureAttributes adorned on the test assembly
            Aggregator.Run(() =>
            {
                var fixturesAttrs = ((IReflectionAssemblyInfo)TestAssembly.Assembly).Assembly
                                                                                    .GetCustomAttributes(typeof(AssemblyFixtureAttribute), false)
                                                                                    .Cast<AssemblyFixtureAttribute>()
                                                                                    .ToList();

                // Instantiate all the fixtures
                foreach (var fixtureAttr in fixturesAttrs)
                    assemblyFixtureMappings[fixtureAttr.FixtureType] = Activator.CreateInstance(fixtureAttr.FixtureType);
            });
        }

        protected override Task BeforeTestAssemblyFinishedAsync()
        {
            // Make sure we clean up everybody who is disposable, and use Aggregator.Run to isolate Dispose failures
            foreach (var disposable in assemblyFixtureMappings.Values.OfType<IDisposable>())
                Aggregator.Run(disposable.Dispose);

            return base.BeforeTestAssemblyFinishedAsync();
        }

        protected override Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus,
                                                                   ITestCollection testCollection,
                                                                   IEnumerable<IXunitTestCase> testCases,
                                                                   CancellationTokenSource cancellationTokenSource)
            => new XunitTestCollectionRunnerWithAssemblyFixture(assemblyFixtureMappings, testCollection, testCases, DiagnosticMessageSink, messageBus, TestCaseOrderer, new ExceptionAggregator(Aggregator), cancellationTokenSource).RunAsync();
    }
}
