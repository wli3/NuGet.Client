// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Experimentation;
using NuGet.Common;

namespace NuGet.VisualStudio
{
    public class NuGetVSExperimentationService : INuGetExperimentationService
    {
        private readonly IExperimentationService _experimentationService;

        public NuGetVSExperimentationService(IExperimentationService experimentationService)
        {
            _experimentationService = experimentationService ?? throw new ArgumentNullException(nameof(experimentationService));
        }

        public bool IsFlightEnabled(string flight)
        {
            return _experimentationService.IsCachedFlightEnabled(flight);
        }
    }
}
