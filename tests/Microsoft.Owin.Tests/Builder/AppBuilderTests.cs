﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Owin;
using Shouldly;
using Xunit;

namespace Microsoft.Owin.Builder.Tests
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class AppBuilderTests
    {
        public delegate string AppOne(string call);

        public delegate string AppTwo(string call);

        [Fact]
        public void DelegateShouldBeCalledToAddMiddlewareAroundTheDefaultApp()
        {
            string theNext = "next";
            string theMiddle = "middle";
            string theDefault = "default";

            Func<string, string> middleware = next =>
            {
                theNext = next;
                return theMiddle;
            };

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;
            var theApp = builder.BuildNew<string>(x => x.Use(middleware));

            builder.Use(new Func<object, object>(ignored => theApp));

            theNext.ShouldBeSameAs(theDefault);
            theApp.ShouldBeSameAs(theMiddle);
            theApp.ShouldNotBeSameAs(theDefault);
        }

        [Fact]
        public void DelegateShouldBeCalledWithParameters()
        {
            string theDefault = "default";

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;
            builder.Use(new Func<string, int, long, string>((s, i, l) => i + l + s), 1, 2L);
            var theApp = builder.Build<string>();

            theApp.ShouldNotBeSameAs("12" + theDefault);
        }

        [Fact]
        public void ConversionShouldBeCalledBetweenDifferentSignatures()
        {
            object theDefault = "42";

            Func<string, int> convert1 = app => int.Parse(app, CultureInfo.InvariantCulture) + 1;
            Func<int, string> convert2 = app => app.ToString(CultureInfo.InvariantCulture) + "2";

            Func<string, string> middleware1 = app => app + "3";
            Func<int, int> middleware2 = app => app + 4;

            var builder = new AppBuilder();
            builder.AddSignatureConversion(convert1);
            builder.AddSignatureConversion(convert2);
            builder.Properties["builder.DefaultApp"] = theDefault;

            var theApp = builder.BuildNew<int>(x => x.Use(middleware1).Use(middleware2));

            // "42" + 1: 43         // theDefault passed through convert1 for next middleware
            // 43 + 4: 47           // passed through middleware2
            // 47 + "2": "472"      // passed through convert2 for next middleware
            // "472" + "3": "4723"  // passed through middleware1
            // "4723" + 1: 4724     // passed through convert1 to return

            theApp.ShouldBe(4724);
        }

        [Fact]
        public void InstanceMemberNamedInvokeShouldQualifyAsMiddlewareFactory()
        {
            Func<int, string> theDefault = call => "Hello[" + call + "]";

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;

            var theApp = builder.BuildNew<Func<int, string>>(
                x => x
                    .Use(new StringPlusValue(" world!"))
                    .Use(new StringPlusValue(" there,")));

            theApp(42).ShouldBe("Hello[42] there, world!");
        }

        [Fact]
        public void DelegateShouldQualifyAsAppWithRun()
        {
            Func<int, string> theDefault = call => "Hello[" + call + "]";
            Func<int, string> theSite = call => "Called[" + call + "]";

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;

            var theApp = builder.BuildNew<Func<int, string>>(x =>
                x.Use(new Func<object, object>(ignored => theSite)));

            theApp(42).ShouldBe("Called[42]");
        }

        [Fact]
        public void InstanceMemberNamedInvokeShouldQualifyAsAppWithRun()
        {
            var theSite = new MySite();

            var builder = new AppBuilder();

            var theApp = builder.BuildNew<Func<int, string>>(x =>
                x.Use(new Func<object, object>(ignored => theSite)));

            theApp(42).ShouldBe("Called[42]");
        }

        [Fact]
        public void TypeofClassConstructorsShouldQualifyAsMiddlewareFactory()
        {
            Func<int, string> theDefault = call => "Hello[" + call + "]";

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;

            var theApp = builder.BuildNew<Func<int, string>>(
                x => x
                    .Use(typeof(StringPlusValue2), " world!")
                    .Use(typeof(StringPlusValue2), " there,"));

            theApp(42).ShouldBe("Hello[42] there, world!");
        }

        [Fact]
        public void GenericClassConstructorsShouldQualifyAsMiddlewareFactory()
        {
            Func<int, string> theDefault = call => "Hello[" + call + "]";

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;

            var theApp = builder.BuildNew<Func<int, string>>(
                x => x
                    .Use<StringPlusValue2>(" world!")
                    .Use<StringPlusValue2>(" there,"));

            theApp(42).ShouldBe("Hello[42] there, world!");
        }

        [Fact]
        public void TypeofClassConstructorsWithWrongParameterCountShouldThrow()
        {
            Func<int, string> theDefault = call => "Hello[" + call + "]";

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;

            Should.Throw<MissingMethodException>(() =>
            {
                var theApp = builder.BuildNew<Func<int, string>>(
                    x => x.Use(typeof(StringPlusValue2), "arg 1", "extra arg"));
            });
        }

        [Fact]
        public void OtherObjectShouldThrow()
        {
            Func<int, string> theDefault = call => "Hello[" + call + "]";

            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = theDefault;

            Should.Throw<NotSupportedException>(() =>
            {
                var theApp = builder.BuildNew<Func<int, string>>(
                    x => x.Use(new object()));
            });
        }

        [Fact]
        public void DelegatesWithIdenticalParametersShouldConvertAutomatically()
        {
            var builder = new AppBuilder();
            builder.Properties["builder.DefaultApp"] = new Func<string, string>(call => call);
            builder.UseFunc<AppOne>(next => call => next(call) + "1");
            builder.UseFunc<AppTwo>(next => call => next(call) + "2");
            builder.UseFunc<Func<string, string>>(next => call => next(call) + "3");
            var app = builder.Build<AppTwo>();
            app("0").ShouldBe("0321");
        }

        [Fact]
        public void TheDefaultDefaultShouldBe404()
        {
            var builder = new AppBuilder();
            var app = builder.Build<OwinMiddleware>();

            var context = new OwinContext();
            app.Invoke(context).Wait();
            context.Response.StatusCode.ShouldBe(404);
        }

        [Fact]
        public void ConverterCombinationWillBeInvokedIfNeeded()
        {
            var builder = new AppBuilder();
            Func<AppFunc, DifferentType> convert1 = _ => new DifferentType();
            Func<DifferentType, AppFunc> convert2 = _ => call => { throw new NotImplementedException(); };
            builder.AddSignatureConversion(convert1);
            builder.AddSignatureConversion(convert2);

            var diff = builder.Build<DifferentType>();
        }

        [Fact]
        public void NoConversionAvailableThrowsArgumentException()
        {
            var builder = new AppBuilder();
            Assert.Throws<ArgumentException>(() => builder.Build<DifferentType>());
        }

        [Fact]
        public void UseAppWithIOwinContext()
        {
            var builder = new AppBuilder();
            builder.Run(context =>
            {
                context.Response.StatusCode = 201;
                return Task.FromResult<object>(null);
            });

            AppFunc theApp = builder.Build();
            IOwinContext baseContext = new OwinContext();
            theApp(baseContext.Environment).Wait();
            Assert.Equal(201, baseContext.Response.StatusCode);
        }

        [Fact]
        public void UseHandlerWithIOwinContext()
        {
            var builder = new AppBuilder();
            builder.Use((context, next) =>
            {
                context.Response.ReasonPhrase = "Set In Middleware";
                return next();
            });

            AppFunc theApp = builder.Build();
            IOwinContext baseContext = new OwinContext();
            theApp(baseContext.Environment).Wait();
            Assert.Equal(404, baseContext.Response.StatusCode);
            Assert.Equal("Set In Middleware", baseContext.Response.ReasonPhrase);
        }

        [Fact]
        public void AppFuncConvertsToOwinMiddleware()
        {
            var builder = new AppBuilder();
            builder.Use(new Func<object, AppFunc>(ignored =>
            {
                return environment =>
                {
                    environment["owin.ResponseReasonPhrase"] = "Set In Middleware";
                    return Task.FromResult<object>(null);
                };
            }));

            var theApp = builder.Build<OwinMiddleware>();
            IOwinContext baseContext = new OwinContext();
            theApp.Invoke(baseContext).Wait();
            Assert.Equal(200, baseContext.Response.StatusCode);
            Assert.Equal("Set In Middleware", baseContext.Response.ReasonPhrase);
        }

        [Fact]
        public void OwinMiddlewareConvertsToAppFunc()
        {
            var builder = new AppBuilder();
            builder.Use(new Func<object, OwinMiddleware>(ignored => { return new OwinMiddlwareApp(); }));

            var theApp = builder.Build<AppFunc>();
            IOwinContext baseContext = new OwinContext();
            theApp.Invoke(baseContext.Environment).Wait();
            Assert.Equal(200, baseContext.Response.StatusCode);
            Assert.Equal("Set In Middleware", baseContext.Response.ReasonPhrase);
        }

        private class StringPlusValue
        {
            private readonly string _value;

            public StringPlusValue(string value)
            {
                _value = value;
            }

            public Func<int, string> Invoke(Func<int, string> app)
            {
                return call => app(call) + _value;
            }
        }

        private class StringPlusValue2
        {
            private readonly Func<int, string> _app;
            private readonly string _value;

            public StringPlusValue2(Func<int, string> app)
            {
                _app = app;
                _value = " PlusPlus";
            }

            public StringPlusValue2(Func<int, string> app, string value)
            {
                _app = app;
                _value = value;
            }

            public string Invoke(int call)
            {
                return _app(call) + _value;
            }
        }

        public class DifferentType
        {
        }

        public class MySite
        {
            public string Invoke(int call)
            {
                return "Called[" + call + "]";
            }
        }

        private class OwinMiddlwareApp : OwinMiddleware
        {
            internal OwinMiddlwareApp()
                : base(null)
            {
            }

            public override Task Invoke(IOwinContext context)
            {
                context.Response.ReasonPhrase = "Set In Middleware";
                return Task.FromResult<object>(null);
            }
        }
    }
}
