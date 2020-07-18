// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Strings = NuGet.Protocol.Strings;

static internal class ODataServiceDocumentUtils
{
    public static async Task<ODataServiceDocumentResourceV2> CreateODataServiceDocumentResourceV2(
        string url,
        HttpSource client,
        DateTime utcNow,
        ILogger log,
        CancellationToken token)
    {
        // Get the service document and record the URL after any redirects.
        string lastRequestUri;
        try
        {
            lastRequestUri = await client.ProcessResponseAsync(
                new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Get, url, log)),
                response =>
                {
                    if (response.RequestMessage == null)
                    {
                        return Task.FromResult(url);
                    }

                    return Task.FromResult(response.RequestMessage.RequestUri.ToString());
                },
                log,
                token);
        }
        catch (Exception ex) when (!(ex is FatalProtocolException) && (!(ex is OperationCanceledException)))
        {
#if NET472
            WebException webEx = ex.InnerException as WebException;
            if (webEx != null && webEx.Status == WebExceptionStatus.NameResolutionFailure)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Http_HostNotFound,
                    url);
                throw new FatalProtocolException(message, ex, NuGetLogCode.NU1305);
            }
#elif NETSTANDARD2_0 || NETCOREAPP5_0
            SocketException sockEx = ex.InnerException as SocketException;
            if (sockEx != null && sockEx.SocketErrorCode == SocketError.HostNotFound)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Http_HostNotFound,
                    url);
                throw new FatalProtocolException(message, ex, NuGetLogCode.NU1305);
            }
#endif
            else
            {
                string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToReadServiceIndex, url);
                throw new FatalProtocolException(message, ex);
            }
        }

        // Trim the query string or any trailing slash.
        var builder = new UriBuilder(lastRequestUri) { Query = null };
        var baseAddress = builder.Uri.AbsoluteUri.Trim('/');

        return new ODataServiceDocumentResourceV2(baseAddress, DateTime.UtcNow);
    }
}
