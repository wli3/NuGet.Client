// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// Stores/caches a service index json file.
    /// </summary>
    public class ServiceIndexResourceV3 : INuGetResource
    {
        private delegate T CreateEntry<T>(Uri serviceUri, string type, SemanticVersion clientVersion, string flight);
        
        private readonly string _json;
        private readonly IDictionary<string, List<ServiceIndexEntry>> _resources;
        private readonly IDictionary<string, List<ServiceIndexEntryFlight>> _flights;
        private readonly DateTime _requestTime;
        private static readonly IReadOnlyList<ServiceIndexEntry> _emptyEntries = new List<ServiceIndexEntry>();
        private static readonly IReadOnlyList<ServiceIndexEntryFlight> _emptyFlightEntries = new List<ServiceIndexEntryFlight>();
        internal static readonly SemanticVersion DefaultClientVersion = new SemanticVersion(0, 0, 0);

        /// <summary>Singleton of NuGet experimentation service instance.</summary>
        public static INuGetExperimentationService NuGetExperimentationService { get; set; } = NullExperimentationService.Instance;

        public ServiceIndexResourceV3(JObject index, DateTime requestTime)
        {
            _json = index.ToString();
            _resources = ParseResources(index);
            _flights = ParseFlights(index);
            _requestTime = requestTime;
        }

        /// <summary>
        /// Time the index was requested
        /// </summary>
        public virtual DateTime RequestTime
        {
            get { return _requestTime; }
        }

        /// <summary>
        /// All service index entries.
        /// </summary>
        public virtual IReadOnlyList<ServiceIndexEntry> Entries
        {
            get
            {
                return _resources.SelectMany(e => e.Value).ToList();
            }
        }

        public virtual string Json
        {
            get
            {
                return _json;
            }
        }

        /// <summary>
        /// Get the list of service entries that best match the current clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<ServiceIndexEntry> GetServiceEntries(params string[] orderedTypes)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion();
            var experimentationService = GetExperimentationService();

            return GetServiceEntries(clientVersion, experimentationService, orderedTypes);
        }

        /// <summary>
        /// Get the list of service entries that best match the client flights, clientVersion, and type.
        /// </summary>
        private IReadOnlyList<ServiceIndexEntry> GetServiceEntries(NuGetVersion clientVersion, INuGetExperimentationService experimentationService, params string[] orderedTypes)
        {
            if (clientVersion == null)
            {
                throw new ArgumentNullException(nameof(clientVersion));
            }

            if (experimentationService == null)
            {
                throw new ArgumentNullException(nameof(experimentationService));
            }

            foreach (var type in orderedTypes)
            {
                List<ServiceIndexEntryFlight> flightsWithDesiredType;
                if (_flights.TryGetValue(type, out flightsWithDesiredType))
                {
                    var matchingFlights = GetMatchesForFlights(experimentationService, flightsWithDesiredType);
                    if (matchingFlights.Count > 0)
                    {
                        return matchingFlights;
                    }
                }

                List<ServiceIndexEntry> entries;
                if (_resources.TryGetValue(type, out entries))
                {
                    var compatible = GetBestVersionMatchForType(clientVersion, entries);

                    if (compatible.Count > 0)
                    {
                        return compatible;
                    }
                }
            }

            return _emptyEntries;
        }

        /// <summary>
        /// Get the list of service entries that best match the clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<ServiceIndexEntry> GetServiceEntries(NuGetVersion clientVersion, params string[] orderedTypes)
        {
            var experimentationService = GetExperimentationService();

            return GetServiceEntries(clientVersion, experimentationService, orderedTypes);
        }

        private static INuGetExperimentationService GetExperimentationService()
        {
            return NuGetExperimentationService ?? NullExperimentationService.Instance;
        }

        private IReadOnlyList<ServiceIndexEntryFlight> GetMatchesForFlights(INuGetExperimentationService experimentationService, List<ServiceIndexEntryFlight> entries)
        {
            // Prefer the first flight matched, as determined by the order in the service index.
            var firstFlight = entries.FirstOrDefault(e => experimentationService.IsFlightEnabled(e.Flight));

            if (firstFlight == null)
            {
                // No matching flights
                return _emptyFlightEntries;
            }
            else
            {
                // Find all entries with the same flight.
                return entries.Where(e => e.Flight == firstFlight.Flight).ToList();
            }
        }

        private IReadOnlyList<ServiceIndexEntry> GetBestVersionMatchForType(NuGetVersion clientVersion, List<ServiceIndexEntry> entries)
        {
            var bestMatch = entries.FirstOrDefault(e => e.ClientVersion <= clientVersion);

            if (bestMatch == null)
            {
                // No compatible version
                return _emptyEntries;
            }
            else
            {
                // Find all entries with the same version.
                return entries.Where(e => e.ClientVersion == bestMatch.ClientVersion).ToList();
            }
        }

        /// <summary>
        /// Get the best match service URI.
        /// </summary>
        public virtual Uri GetServiceEntryUri(params string[] orderedTypes)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion();
            var experimentationService = GetExperimentationService();

            return GetServiceEntryUris(clientVersion, experimentationService, orderedTypes).FirstOrDefault();
        }

        /// <summary>
        /// Get the list of service URIs that best match the current clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<Uri> GetServiceEntryUris(params string[] orderedTypes)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion();

            return GetServiceEntryUris(clientVersion, orderedTypes);
        }

        /// <summary>
        /// Get the list of service URIs that best match the clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<Uri> GetServiceEntryUris(NuGetVersion clientVersion, params string[] orderedTypes)
        {
            var experimentationService = GetExperimentationService();

            return GetServiceEntryUris(clientVersion, experimentationService, orderedTypes);
        }

        private IReadOnlyList<Uri> GetServiceEntryUris(NuGetVersion clientVersion, INuGetExperimentationService experimentationService, string[] orderedTypes)
        {
            if (clientVersion == null)
            {
                throw new ArgumentNullException(nameof(clientVersion));
            }

            if (experimentationService == null)
            {
                throw new ArgumentNullException(nameof(experimentationService));
            }

            return GetServiceEntries(clientVersion, experimentationService, orderedTypes).Select(e => e.Uri).ToList();
        }

        private static IDictionary<string, List<ServiceIndexEntry>> ParseResources(JObject index)
        {
            var result = new Dictionary<string, List<ServiceIndexEntry>>(StringComparer.Ordinal);

            if (index.TryGetValue("resources", out JToken resources))
            {
                AddEntries(result, resources, (uri, type, version, flight) => new ServiceIndexEntry(uri, type, version));
            }

            // Order versions desc for faster lookup later.
            foreach (var type in result.Keys.ToArray())
            {
                result[type] = result[type].OrderByDescending(e => e.ClientVersion).ToList();
            }

            return result;
        }

        private static IDictionary<string, List<ServiceIndexEntryFlight>> ParseFlights(JObject index)
        {
            var result = new Dictionary<string, List<ServiceIndexEntryFlight>>(StringComparer.Ordinal);

            if (index.TryGetValue("flights", out JToken flights))
            {
                AddEntries(result, flights, (uri, type, version, flight) => new ServiceIndexEntryFlight(uri, type, flight));
            }

            return result;
        }

        private static void AddEntries<T>(
            Dictionary<string, List<T>> result,
            JToken resources,
            CreateEntry<T> createEntry)
        {
            foreach (var resource in resources)
            {
                var id = GetValues(resource["@id"]).SingleOrDefault();

                Uri uri;
                if (string.IsNullOrEmpty(id) || !Uri.TryCreate(id, UriKind.Absolute, out uri))
                {
                    // Skip invalid or missing @ids
                    continue;
                }

                var types = GetValues(resource["@type"]).ToArray();
                var clientVersionToken = resource["clientVersion"];

                var clientVersions = new List<SemanticVersion>();

                if (clientVersionToken == null)
                {
                    // For non-versioned services assume all clients are compatible
                    clientVersions.Add(DefaultClientVersion);
                }
                else
                {
                    // Parse supported versions
                    foreach (var versionString in GetValues(clientVersionToken))
                    {
                        SemanticVersion version;
                        if (SemanticVersion.TryParse(versionString, out version))
                        {
                            clientVersions.Add(version);
                        }
                    }
                }

                var flight = GetValues(resource["flight"]).SingleOrDefault();

                // Create service entries
                foreach (var type in types)
                {
                    foreach (var clientVersion in clientVersions)
                    {
                        List<T> entries;
                        if (!result.TryGetValue(type, out entries))
                        {
                            entries = new List<T>();
                            result.Add(type, entries);
                        }

                        entries.Add(createEntry(uri, type, clientVersion, flight));
                    }
                }
            }
        }

        /// <summary>
        /// Read string values from an array or string.
        /// Returns an empty enumerable if the value is null.
        /// </summary>
        private static IEnumerable<string> GetValues(JToken token)
        {
            if (token?.Type == JTokenType.Array)
            {
                foreach (var entry in token)
                {
                    if (entry.Type == JTokenType.String)
                    {
                        yield return entry.ToObject<string>();
                    }
                }
            }
            else if (token?.Type == JTokenType.String)
            {
                yield return token.ToObject<string>();
            }
        }
    }
}
