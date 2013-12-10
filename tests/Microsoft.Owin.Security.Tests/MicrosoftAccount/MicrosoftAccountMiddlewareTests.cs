﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.MicrosoftAccount;
using Microsoft.Owin.Testing;
using Newtonsoft.Json;
using Owin;
using Shouldly;
using Xunit;

namespace Microsoft.Owin.Security.Tests.MicrosoftAccount
{
    public class MicrosoftAccountMiddlewareTests
    {
        [Fact]
        public async Task ChallengeWillTriggerApplyRedirectEvent()
        {
            var options = new MicrosoftAccountAuthenticationOptions()
            {
                ClientId = "Test Client Id",
                ClientSecret = "Test Client Secret",
                Provider = new MicrosoftAccountAuthenticationProvider
                {
                    OnApplyRedirect = context =>
                    {
                        context.Response.Redirect(context.RedirectUri + "&custom=test");
                    }
                }
            };
            var server = CreateServer(
                app => app.UseMicrosoftAccountAuthentication(options),
                context =>
                {
                    context.Authentication.Challenge("Microsoft");
                    return true;
                });
            var transaction = await SendAsync(server, "http://example.com/challenge");
            transaction.Response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            var query = transaction.Response.Headers.Location.Query;
            query.ShouldContain("custom=test");
        }

        [Fact]
        public async Task ChallengeWillTriggerRedirection()
        {
            var server = CreateServer(
                app => app.UseMicrosoftAccountAuthentication("Test Client Id", "Test Client Secret"),
                context =>
                {
                    context.Authentication.Challenge("Microsoft");
                    return true;
                });
            var transaction = await SendAsync(server, "http://example.com/challenge");
            transaction.Response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            var location = transaction.Response.Headers.Location.AbsoluteUri;
            location.ShouldContain("https://login.live.com/oauth20_authorize.srf");
            location.ShouldContain("response_type=code");
            location.ShouldContain("client_id=");
            location.ShouldContain("redirect_uri=");
            location.ShouldContain("scope=");
            location.ShouldContain("state=");
        }

        [Fact]
        public async Task AuthenticatedEventCanGetRefreshToken()
        {
            var options = new MicrosoftAccountAuthenticationOptions()
            {
                ClientId = "Test Client Id",
                ClientSecret = "Test Client Secret",
                BackchannelHttpHandler = new TestHttpMessageHandler
                {
                    Sender = async req =>
                    {
                        if (req.RequestUri.AbsoluteUri == "https://login.live.com/oauth20_token.srf")
                        {
                            return await ReturnJsonResponse(new
                            {
                                access_token = "Test Access Token",
                                expire_in = 3600,
                                token_type = "Bearer",
                                refresh_token = "Test Refresh Token"
                            });
                        }
                        else if (req.RequestUri.GetLeftPart(UriPartial.Path) == "https://apis.live.net/v5.0/me")
                        {
                            return await ReturnJsonResponse(new
                            {
                                id = "Test User ID",
                                name = "Test Name",
                                first_name = "Test Given Name",
                                last_name = "Test Family Name",
                                emails = new
                                {
                                    preferred = "Test email"
                                }
                            });
                        }

                        return null;
                    }
                },
                Provider = new MicrosoftAccountAuthenticationProvider
                {
                    OnAuthenticated = context =>
                    {
                        var refreshToken = context.RefreshToken;
                        context.Identity.AddClaim(new Claim("RefreshToken", refreshToken));
                        return Task.FromResult<object>(null);
                    }
                }
            };
            var server = CreateServer(
                app => app.UseMicrosoftAccountAuthentication(options),
                context =>
                {
                    var req = context.Request;
                    var res = context.Response;
                    Describe(res, new AuthenticateResult(req.User.Identity, new AuthenticationProperties(), new AuthenticationDescription()));
                    return true;
                });
            var properties = new AuthenticationProperties();
            var correlationKey = ".AspNet.Correlation.Microsoft";
            var correlationValue = "TestCorrelationId";
            properties.Dictionary.Add(correlationKey, correlationValue);
            properties.RedirectUri = "/me";
            var state = options.StateDataFormat.Protect(properties);
            var transaction = await SendAsync(server,
                "https://example.com/signin-microsoft?code=TestCode&state=" + Uri.EscapeDataString(state),
                correlationKey + "=" + correlationValue);
            transaction.Response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            transaction.Response.Headers.Location.ToString().ShouldBe("/me");
            transaction.SetCookie.Count.ShouldBe(2);
            transaction.SetCookie[0].ShouldContain(correlationKey);
            transaction.SetCookie[1].ShouldContain(".AspNet.External");

            var authCookie = transaction.AuthenticationCookieValue;
            transaction = await SendAsync(server, "https://example.com/me", authCookie);
            transaction.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
            transaction.FindClaimValue("RefreshToken").ShouldBe("Test Refresh Token");
        }

        private static TestServer CreateServer(Action<IAppBuilder> configure, Func<IOwinContext, bool> handler)
        {
            return TestServer.Create(app =>
            {
                app.Properties["host.AppName"] = "Microsoft.Owin.Security.Tests";
                app.UseCookieAuthentication(new CookieAuthenticationOptions
                {
                    AuthenticationType = "External"
                });
                app.SetDefaultSignInAsAuthenticationType("External");
                if (configure != null)
                {
                    configure(app);
                }
                app.Use(async (context, next) =>
                {
                    if (handler == null || !handler(context))
                    {
                        await next();
                    }
                });
            });
        }

        private static async Task<Transaction> SendAsync(TestServer server, string uri, string cookieHeader = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.Add("Cookie", cookieHeader);
            }
            var transaction = new Transaction
            {
                Request = request,
                Response = await server.HttpClient.SendAsync(request),
            };
            if (transaction.Response.Headers.Contains("Set-Cookie"))
            {
                transaction.SetCookie = transaction.Response.Headers.GetValues("Set-Cookie").ToList();
            }
            transaction.ResponseText = await transaction.Response.Content.ReadAsStringAsync();

            if (transaction.Response.Content != null &&
                transaction.Response.Content.Headers.ContentType != null &&
                transaction.Response.Content.Headers.ContentType.MediaType == "text/xml")
            {
                transaction.ResponseElement = XElement.Parse(transaction.ResponseText);
            }
            return transaction;
        }

        private static async Task<HttpResponseMessage> ReturnJsonResponse(object content)
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            var text = await JsonConvert.SerializeObjectAsync(content);
            res.Content = new StringContent(text, Encoding.UTF8, "application/json");
            return res;
        }

        private static void Describe(IOwinResponse res, AuthenticateResult result)
        {
            res.StatusCode = 200;
            res.ContentType = "text/xml";
            var xml = new XElement("xml");
            if (result != null && result.Identity != null)
            {
                xml.Add(result.Identity.Claims.Select(claim => new XElement("claim", new XAttribute("type", claim.Type), new XAttribute("value", claim.Value))));
            }
            if (result != null && result.Properties != null)
            {
                xml.Add(result.Properties.Dictionary.Select(extra => new XElement("extra", new XAttribute("type", extra.Key), new XAttribute("value", extra.Value))));
            }
            using (var memory = new MemoryStream())
            {
                using (var writer = new XmlTextWriter(memory, Encoding.UTF8))
                {
                    xml.WriteTo(writer);
                }
                res.Body.Write(memory.ToArray(), 0, memory.ToArray().Length);
            }
        }

        private class TestHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, Task<HttpResponseMessage>> Sender { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                if (Sender != null)
                {
                    return await Sender(request);
                }

                return null;
            }
        }

        private class Transaction
        {
            public HttpRequestMessage Request { get; set; }
            public HttpResponseMessage Response { get; set; }
            public IList<string> SetCookie { get; set; }
            public string ResponseText { get; set; }
            public XElement ResponseElement { get; set; }

            public string AuthenticationCookieValue
            {
                get
                {
                    if (SetCookie != null && SetCookie.Count > 0)
                    {
                        var authCookie = SetCookie.SingleOrDefault(c => c.Contains(".AspNet.External="));
                        if (authCookie != null)
                        {
                            return authCookie.Substring(0, authCookie.IndexOf(';'));
                        }
                    }

                    return null;
                }
            }

            public string FindClaimValue(string claimType)
            {
                XElement claim = ResponseElement.Elements("claim").SingleOrDefault(elt => elt.Attribute("type").Value == claimType);
                if (claim == null)
                {
                    return null;
                }
                return claim.Attribute("value").Value;
            }
        }
    }
}
