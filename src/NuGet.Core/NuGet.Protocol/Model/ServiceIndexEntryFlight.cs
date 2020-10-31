// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol
{
    /// <summary>
    /// index.json entry in the "flights" section.
    /// </summary>
    internal class ServiceIndexEntryFlight : ServiceIndexEntry
    {
        /// <summary>
        /// The name of the flight that this service index entry applied to.
        /// </summary>
        public string Flight { get; }

        public ServiceIndexEntryFlight(
            Uri serviceUri,
            string serviceType,
            string flight) : base(
                serviceUri,
                serviceType,
                ServiceIndexResourceV3.DefaultClientVersion)
        {
            if (flight == null)
            {
                throw new ArgumentNullException(nameof(flight));
            }

            Flight = flight;
        }
    }
}
