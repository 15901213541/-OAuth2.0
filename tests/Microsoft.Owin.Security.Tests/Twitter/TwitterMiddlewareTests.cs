﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Twitter;
using Microsoft.Owin.Testing;
using Newtonsoft.Json;
using Owin;
using Shouldly;
using Xunit;

namespace Microsoft.Owin.Security.Tests.Twitter
{
    public class TwitterMiddlewareTests
    {
        [Fact]
        public async Task ChallengeWillTriggerApplyRedirectEvent()
        {
            var options = new TwitterAuthenticationOptions()
            {
                ConsumerKey = "Test Consumer Key",
                ConsumerSecret = "Test Consumer Secret",
                Provider = new TwitterAuthenticationProvider
                {
                    OnApplyRedirect = context =>
                    {
                        context.Response.Redirect(context.RedirectUri + "&custom=test");
                    }
                },
                BackchannelHttpHandler = new TestHttpMessageHandler
                {
                    Sender = req =>
                        {
                            if (req.RequestUri.AbsoluteUri == "https://api.twitter.com/oauth/request_token")
                            {
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                                {
                                    Content =
                                        new StringContent("oauth_callback_confirmed=true&oauth_token=test_oauth_token&oauth_token_secret=test_oauth_token_secret",
                                            Encoding.UTF8,
                                            "application/x-www-form-urlencoded")
                                });
                            }
                            return Task.FromResult<HttpResponseMessage>(null);
                        }
                },
                BackchannelCertificateValidator = null
            };
            var server = CreateServer(
                app => app.UseTwitterAuthentication(options),
                context =>
                {
                    context.Authentication.Challenge("Twitter");
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
            var options = new TwitterAuthenticationOptions()
            {
                ConsumerKey = "Test Consumer Key",
                ConsumerSecret = "Test Consumer Secret",
                BackchannelHttpHandler = new TestHttpMessageHandler
                {
                    Sender = req =>
                    {
                        if (req.RequestUri.AbsoluteUri == "https://api.twitter.com/oauth/request_token")
                        {
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content =
                                    new StringContent("oauth_callback_confirmed=true&oauth_token=test_oauth_token&oauth_token_secret=test_oauth_token_secret",
                                        Encoding.UTF8,
                                        "application/x-www-form-urlencoded")
                            });
                        }
                        return Task.FromResult<HttpResponseMessage>(null);
                    }
                },
                BackchannelCertificateValidator = null
            };
            var server = CreateServer(
                app => app.UseTwitterAuthentication(options),
                context =>
                {
                    context.Authentication.Challenge("Twitter");
                    return true;
                });
            var transaction = await SendAsync(server, "http://example.com/challenge");
            transaction.Response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            var location = transaction.Response.Headers.Location.AbsoluteUri;
            location.ShouldContain("https://twitter.com/oauth/authenticate?oauth_token=");
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

            return transaction;
        }

        private static async Task<HttpResponseMessage> ReturnJsonResponse(object content)
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK);
            var text = await JsonConvert.SerializeObjectAsync(content);
            res.Content = new StringContent(text, Encoding.UTF8, "application/json");
            return res;
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
        }
    }
}
