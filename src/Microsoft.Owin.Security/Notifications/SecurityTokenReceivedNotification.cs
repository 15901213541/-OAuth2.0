﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace Microsoft.Owin.Security
{
    public class SecurityTokenReceivedNotification
    {
        public SecurityTokenReceivedNotification() { }
        public bool Cancel { get; set; }
        public string SecurityToken { get; set; }
        public int StatusCode {get;set;}
    }
}