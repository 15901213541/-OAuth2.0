﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using FunctionalTests.Common;
using Xunit;
using Xunit.Extensions;

namespace FunctionalTests.Facts.Discovery
{
    public class InvalidConfigurationMethodSignatureTest
    {
        [Theory, Trait("FunctionalTests", "General")]
        [InlineData(HostType.IIS)]
        [InlineData(HostType.HttpListener)]
        public void InvalidConfigurationMethodSignature(HostType hostType)
        {
            var expectedExceptionType = typeof(EntryPointNotFoundException);
            using (ApplicationDeployer deployer = new ApplicationDeployer())
            {
                if (hostType != HostType.IIS)
                {
                    Assert.Throws(expectedExceptionType, () => deployer.Deploy<InvalidConfigurationMethodSignatureTest>(hostType));
                }
                else
                {
                    string applicationUrl = deployer.Deploy<InvalidConfigurationMethodSignatureTest>(hostType);
                    Assert.True(HttpClientUtility.GetResponseTextFromUrl(applicationUrl).Contains(expectedExceptionType.Name), "Fatal error not thrown with invalid Configuration method signature");
                }
            }
        }

        public void Configuration(object app)
        {
        }
    }
}
