// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Shared;

namespace NuGet.Frameworks
{
    public class MultipleCompatibilityFramework : NuGetFramework
    {
        private int? _hashCode;

        /// <summary>
        /// Root project framework.
        /// </summary>
        public NuGetFramework RootFramework { get; private set; }
        public NuGetFramework SecondaryFramework { get; private set; }

        public MultipleCompatibilityFramework(NuGetFramework framework, NuGetFramework secondaryFramework)
            : base(ValidateFrameworkArgument(framework))
        {
            if (secondaryFramework == null)
            {
                throw new ArgumentNullException("secondaryFramework");
            }

            SecondaryFramework = secondaryFramework;
            RootFramework = framework;
        }

        /// <summary>
        /// Create a FallbackFramework from the current AssetTargetFallbackFramework.
        /// </summary>
        public FallbackFramework AsFallbackFramework()
        {
            return new FallbackFramework(RootFramework, new NuGetFramework[] { SecondaryFramework });
        }



        private static NuGetFramework ValidateFrameworkArgument(NuGetFramework framework)
        {
            if (framework == null)
            {
                throw new ArgumentNullException("framework");
            }
            return framework;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MultipleCompatibilityFramework);
        }

        public override int GetHashCode()
        {
            if (_hashCode == null)
            {
                var combiner = new HashCodeCombiner();

                // Ensure that this is different from a FallbackFramework;
                combiner.AddStringIgnoreCase("multipleCompat");

                combiner.AddObject(Comparer.GetHashCode(this));


                combiner.AddObject(Comparer.GetHashCode(SecondaryFramework));
                _hashCode = combiner.CombinedHash;
            }

            return _hashCode.Value;
        }

        public bool Equals(MultipleCompatibilityFramework other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return NuGetFramework.Comparer.Equals(this, other)
                && SecondaryFramework.Equals(other.SecondaryFramework);
        }
    }
}
