// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    public class NullExperimentationService : INuGetExperimentationService
    {
        public static NullExperimentationService Instance { get; } = new NullExperimentationService();

        public bool IsFlightEnabled(string flight)
        {
            return false;
        }
    }
}
