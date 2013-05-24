// <copyright file="FederationAuthenticationProvider.cs" company="Microsoft Open Technologies, Inc.">
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

using System;
using System.Threading.Tasks;

namespace Microsoft.Owin.Security.Federation
{
    public class FederationAuthenticationProvider : IFederationAuthenticationProvider
    {
        public FederationAuthenticationProvider()
        {
            OnSecurityTokenReceived = context => Task.FromResult<object>(null);
            OnSecurityTokenValidated = context => Task.FromResult<object>(null);
        }

        public Func<SecurityTokenReceivedContext, Task> OnSecurityTokenReceived { get; set; }

        public Func<SecurityTokenValidatedContext, Task> OnSecurityTokenValidated { get; set; }

        public virtual Task SecurityTokenReceived(SecurityTokenReceivedContext context)
        {
            return OnSecurityTokenReceived(context);
        }

        public virtual Task SecurityTokenValidated(SecurityTokenValidatedContext context)
        {
            return OnSecurityTokenValidated(context);
        }
    }
}
