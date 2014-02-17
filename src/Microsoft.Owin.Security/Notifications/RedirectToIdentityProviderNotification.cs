﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Owin.Security.Notifications
{
    public class RedirectToIdentityProviderNotification<TMessage>
    {
        public RedirectToIdentityProviderNotification()
        {
        }

        public bool Cancel { get; set; }
        public TMessage ProtocolMessage { get; set; }
        public int StatusCode { get; set; }
    }
}