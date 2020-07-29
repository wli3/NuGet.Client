// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using NuGet.Protocol.Core.Types;

namespace Test.Utility
{
    public class TestHttpHandler : HttpHandlerResource
    {
        private HttpMessageHandler _messageHandler;

        public TestHttpHandler(HttpMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
        }

        public override HttpMessageHandler ClientHandler
        {
            get { return _messageHandler; }
        }

        public override HttpMessageHandler MessageHandler
        {
            get
            {
                return _messageHandler;
            }
        }
    }
}
