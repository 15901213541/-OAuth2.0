﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Owin.Hosting.Utilities;
using Xunit;

namespace Microsoft.Owin.Hosting.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void LoadSettingsFromConfig_CaseInsensitive()
        {
            IDictionary<string, string> settings = SettingsLoader.LoadFromConfig();
            string value;
            Assert.True(settings.TryGetValue("UpperCase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));

            Assert.True(settings.TryGetValue("uppercase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));
        }

        [Fact]
        public void LoadOptionsFromConfig_CaseInsensitive()
        {
            var options = new StartOptions();
            SettingsLoader.LoadFromConfig(options.Settings);
            IDictionary<string, string> settings = options.Settings;
            string value;
            Assert.True(settings.TryGetValue("UpperCase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));

            Assert.True(settings.TryGetValue("uppercase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));
        }

        [Fact]
        public void LoadSettingsFromFile_CaseInsensitive()
        {
            IDictionary<string, string> settings = SettingsLoader.LoadFromSettingsFile("Settings.txt");
            string value;
            Assert.True(settings.TryGetValue("UpperCase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));

            Assert.True(settings.TryGetValue("uppercase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));
        }

        [Fact]
        public void LoadOptionsFromFile_CaseInsensitive()
        {
            var options = new StartOptions();
            SettingsLoader.LoadFromSettingsFile("Settings.txt", options.Settings);
            IDictionary<string, string> settings = options.Settings;
            string value;
            Assert.True(settings.TryGetValue("UpperCase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));

            Assert.True(settings.TryGetValue("uppercase", out value));
            Assert.True(string.Equals("UpperCaseValue", value, StringComparison.Ordinal));
        }
    }
}
