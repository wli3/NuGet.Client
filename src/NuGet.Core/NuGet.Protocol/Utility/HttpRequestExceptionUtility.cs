// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;

namespace NuGet.Protocol.Core.Types
{
    internal static class HttpRequestExceptionUtility
    {
        internal static HttpStatusCode? GetHttpStatusCode(HttpRequestException ex)
        {
            HttpStatusCode? statusCode = null;

#if NETCOREAPP5_0
            statusCode = ex.StatusCode;
#else
            // HttpRequestException (until .net 5.0, doesn't have ability get status code from http response), so we parse exception string.
            var searchFor = ": 40";
            int searchForLength = searchFor.Length;
            int statusCodeStart = ex.Message.IndexOf(searchFor, 0, StringComparison.OrdinalIgnoreCase);

            if (statusCodeStart >= 0
                && ex.Message.Length > statusCodeStart + searchForLength + 1)
            {
                string errorCode = ex.Message.Substring(statusCodeStart + searchForLength, 1);
                switch (errorCode)
                {
                    case "1":
                        statusCode = HttpStatusCode.Unauthorized;
                        break;
                    case "3":
                        statusCode = HttpStatusCode.Forbidden;
                        break;
                    case "4":
                        statusCode = HttpStatusCode.NotFound;
                        break;
                    case "7":
                        statusCode = HttpStatusCode.ProxyAuthenticationRequired;
                        break;
                }
            }
#endif
            return statusCode;
        }

        internal static void ThrowFatalProtocolExceptionIfCritical(HttpRequestException ex, string url)
        {
            HttpStatusCode? statusCode = GetHttpStatusCode(ex);
            string message = null;

            // For these status codes, we throw exception.
            // Ideally, we add more status codes to this switch statement as we run into other codes that
            // will benifit with a better error experience.
            switch (statusCode)
            {
                case HttpStatusCode.Unauthorized:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_CredentialsForUnauthorized, url);
                    break;
                case HttpStatusCode.Forbidden:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_CredentialsForForbidden, url);
                    break;
                case HttpStatusCode.NotFound:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_CredentialsForNotFound, url);
                    break;
                case HttpStatusCode.ProxyAuthenticationRequired:
                    message = string.Format(CultureInfo.CurrentCulture, Strings.Http_CredentialsForProxy, url);
                    break;
            }

            if (message != null)
            {
                throw new FatalProtocolException(message + " " + ex.Message, statusCode.Value);
            }
        }
    }
}
