// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security;

namespace NuGet.Common
{
    public class EnvironmentVariableWrapper : IEnvironmentVariableReader
    {
        public static IEnvironmentVariableReader Instance { get; } = new EnvironmentVariableWrapper();

        public string GetEnvironmentVariable(string variable)
        {
            var MaxTries = 10;

            for (var i = 0; i < MaxTries; i++)
            {
                try
                {
                    var envVariable = Environment.GetEnvironmentVariable(variable);
                    if (envVariable != null)
                    {
                        return envVariable;
                    }
                }
                catch (SecurityException)
                {
                    return null;
                }
            }
            return null;
        }
    }
}
