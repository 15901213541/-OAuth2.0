// <copyright file="FederationAuthenticationContext.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IdentityModel.Configuration;
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Services;
using System.IdentityModel.Services.Configuration;
using System.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.Infrastructure;

namespace Microsoft.Owin.Security.Federation
{
    internal class FederationAuthenticationHandler : AuthenticationHandler<FederationAuthenticationOptions>
    {
        private readonly ILogger _logger;
        private readonly FederationConfiguration _federationConfiguration;

        public FederationAuthenticationHandler(ILogger logger, FederationConfiguration federationConfiguration)
        {
            _logger = logger;
            _federationConfiguration = federationConfiguration;
        }

        public override async Task<bool> InvokeAsync()
        {
            return await InvokeReplyPathAsync();
        }

        protected override async Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            if (!string.Equals(Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var form = await Request.ReadFormAsync();
            var nameValueForm = ConvertToNameValueCollection(form);

            WSFederationMessage message = WSFederationMessage.CreateFromNameValueCollection(
                new Uri(_federationConfiguration.WsFederationConfiguration.Realm),
                nameValueForm);

            var signIn = message as SignInResponseMessage;
            if (signIn == null)
            {
                return null;
            }

            var extra = Options.StateDataFormat.Unprotect(message.Context);
            if (extra == null)
            {
                return null;
            }

            // OAuth2 10.12 CSRF
            if (!ValidateCorrelationId(extra, _logger))
            {
                return new AuthenticationTicket(null, extra);
            }

            XmlDictionaryReader xmlReader = XmlDictionaryReader.CreateTextReader(Encoding.UTF8.GetBytes(signIn.Result), XmlDictionaryReaderQuotas.Max);
            var federationSerializer = new WSFederationSerializer(xmlReader);
            var serializationContext = new WSTrustSerializationContext(_federationConfiguration.IdentityConfiguration.SecurityTokenHandlerCollectionManager);
            RequestSecurityTokenResponse securityTokenResponse = federationSerializer.CreateResponse(signIn, serializationContext);
            string xml = securityTokenResponse.RequestedSecurityToken.SecurityTokenXml.OuterXml;

            SecurityToken securityToken = ReadToken(xml);

            var securityTokenReceivedContext = new SecurityTokenReceivedContext(securityToken);
            await Options.Provider.SecurityTokenReceived(securityTokenReceivedContext);

            ClaimsPrincipal principal = AuthenticateToken(securityToken, Request.Uri.AbsoluteUri);

            var securityTokenValidatedContext = new SecurityTokenValidatedContext(principal);
            await Options.Provider.SecurityTokenValidated(securityTokenValidatedContext);

            return new AuthenticationTicket(
                securityTokenValidatedContext.ClaimsPrincipal.Identities.FirstOrDefault(),
                extra);
        }

        private static NameValueCollection ConvertToNameValueCollection(IFormCollection form)
        {
            NameValueCollection collection = new NameValueCollection();
            foreach (var pair in form)
            {
                foreach (var value in pair.Value)
                {
                    // Expected to fail if there's more than one value.
                    collection.Add(pair.Key, value);
                }
            }
            return collection;
        }

        protected override Task ApplyResponseChallengeAsync()
        {
            if (Response.StatusCode != 401)
            {
                return Task.FromResult<object>(null);
            }

            var challenge = Helper.LookupChallenge(Options.AuthenticationType, Options.AuthenticationMode);

            if (challenge != null)
            {
                var extra = new AuthenticationProperties();

                string issuer = _federationConfiguration.WsFederationConfiguration.Issuer;
                string realm = _federationConfiguration.WsFederationConfiguration.Realm;
                var message = new SignInRequestMessage(new Uri(issuer), realm);
                message.Freshness = _federationConfiguration.WsFederationConfiguration.Freshness;
                message.CurrentTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture) + "Z";
                message.AuthenticationType = _federationConfiguration.WsFederationConfiguration.AuthenticationType;
                message.HomeRealm = _federationConfiguration.WsFederationConfiguration.HomeRealm;
                message.Policy = _federationConfiguration.WsFederationConfiguration.Policy;
                message.Reply = _federationConfiguration.WsFederationConfiguration.Reply;
                message.Resource = _federationConfiguration.WsFederationConfiguration.Resource;
                message.Request = _federationConfiguration.WsFederationConfiguration.Request;
                message.RequestPtr = _federationConfiguration.WsFederationConfiguration.RequestPtr;

                string prefix = Request.Scheme + "://" + Request.Host + Request.PathBase;

                string redirectUri = prefix + Request.Path + Request.QueryString;

                extra.RedirectUri = redirectUri;

                // anti csrf
                GenerateCorrelationId(extra);

                message.Context = Options.StateDataFormat.Protect(extra);

                Response.Redirect(message.RequestUrl);
            }

            return Task.FromResult<object>(null);
        }

        public async Task<bool> InvokeReplyPathAsync()
        {
            if (Options.ReturnPath.HasValue && Options.ReturnPath == Request.Path)
            {
                var model = await AuthenticateAsync();

                if (model == null)
                {
                    return false;
                }

                string redirectUri = model.Properties.RedirectUri;

                if (!string.IsNullOrEmpty(Options.SignInAsAuthenticationType))
                {
                    ClaimsIdentity grantIdentity = model.Identity;
                    if (!string.Equals(grantIdentity.AuthenticationType, Options.SignInAsAuthenticationType, StringComparison.Ordinal))
                    {
                        grantIdentity = new ClaimsIdentity(grantIdentity.Claims, Options.SignInAsAuthenticationType, grantIdentity.NameClaimType, grantIdentity.RoleClaimType);
                    }

                    Context.Authentication.SignIn(model.Properties, grantIdentity);
                }
                Response.Redirect(redirectUri);
                return true;
            }
            return false;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed by XmlReader")]
        private SecurityToken ReadToken(string text)
        {
            using (XmlReader reader = XmlReader.Create(new StringReader(text)))
            {
                reader.MoveToContent();

                SecurityTokenHandlerCollection handlers = _federationConfiguration.IdentityConfiguration.SecurityTokenHandlers;
                return handlers.CanReadToken(reader) ? handlers.ReadToken(reader) : null;
            }
        }

        private ClaimsPrincipal AuthenticateToken(SecurityToken token, string resourceName)
        {
            IdentityConfiguration identityConfiguration = _federationConfiguration.IdentityConfiguration;
            var incomingPrincipal = new ClaimsPrincipal(identityConfiguration.SecurityTokenHandlers.ValidateToken(token));
            return identityConfiguration.ClaimsAuthenticationManager.Authenticate(resourceName, incomingPrincipal);
        }
    }
}
