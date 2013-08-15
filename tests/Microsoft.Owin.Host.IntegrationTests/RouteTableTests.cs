﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Routing;
using Owin;
using Shouldly;
using Xunit.Extensions;

#if NET40
namespace Microsoft.Owin.Host40.IntegrationTests
#else

namespace Microsoft.Owin.Host45.IntegrationTests
#endif
{
    public class RouteTableTests : TestBase
    {
        public void SimpleOwinRoute(IAppBuilder ignored)
        {
            RouteTable.Routes.MapOwinRoute("simple", app => app.Run(context => { return context.Response.WriteAsync("Hello world!"); }));
        }

        public void OneSomethingThree(IAppBuilder ignored)
        {
            RouteTable.Routes.MapOwinRoute("one/{something}/three", app => app.Run(context =>
            {
                var httpContext = context.Get<System.Web.HttpContextBase>("System.Web.HttpContextBase");
                RouteValueDictionary values = httpContext.Request.RequestContext.RouteData.Values;
                return context.Response.WriteAsync("Hello, " + values["something"]);
            }));
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.SystemWeb")]
        public Task ItShouldMatchSimpleRoute(string serverName)
        {
            int port = RunWebServer(
                serverName,
                SimpleOwinRoute,
                "web.routetable.config");

            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            return client.GetStringAsync("http://localhost:" + port + "/simple")
                         .Then(response => response.ShouldBe("Hello world!"));
        }

        [Theory]
        [InlineData("Microsoft.Owin.Host.SystemWeb")]
        public Task RouteUrlMayContainDataTokens(string serverName)
        {
            int port = RunWebServer(
                serverName,
                OneSomethingThree,
                "web.routetable.config");

            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            return client.GetStringAsync("http://localhost:" + port + "/one/two/three")
                         .Then(response => response.ShouldBe("Hello, two"));
        }
    }
}
