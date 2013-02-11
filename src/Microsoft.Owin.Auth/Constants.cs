﻿// <copyright file="Constants.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace Microsoft.Owin.Auth
{
    internal static class Constants
    {
        internal const string RequestSchemeKey = "owin.RequestScheme";
        internal const string RequestHeadersKey = "owin.RequestHeaders";
        internal const string ResponseHeadersKey = "owin.ResponseHeaders";
        internal const string ResponseStatusCodeKey = "owin.ResponseStatusCode";
        internal const string ResponseReasonPhraseKey = "owin.ResponseReasonPhrase";

        internal const string ServerUserKey = "server.User";
        internal const string ServerOnSendingHeadersKey = "server.OnSendingHeaders";

        internal const string WwwAuthenticateHeader = "WWW-Authenticate";
        internal const string AuthorizationHeader = "Authorization";
    }
}
