// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class PackageOwnershipRule : IPackageVerifierRule
    {
        // Prefixes explicitly reserved for the ASP.Net team.
        private static readonly string[] OwnedPrefixes = new[]
        {
            "Microsoft.AspNetCore.",
            "Microsoft.AspNet.",
            "Microsoft.CodeAnalysis.Razor",
            "Microsoft.Data.Sqlite.",
            "Microsof.Dotnet.",
            "Microsoft.Extensions.",
            "Microsoft.EntityFrameworkCore.",
            "Microsoft.Net.Http.",
            "Microsoft.Owin.",
            "Microsoft.VisualStudio.",
        };

        // Packages that are owned by ASP.Net but do not start with one of the reserved prefixes.
        private static readonly string[] OwnedPackageIds = new string[]
        {
        };

        // Packages that start with one of the reserved prefixes but are not owned by Microsoft or ASP.Net.
        private static readonly string[] ExternallyOwnedPackageIds = new string[]
        {
            "Microsoft.AspNet.Authentication.QQ",
            "Microsoft.AspNet.Authentication.WeChat",
            "Microsoft.AspNet.Diagnostics.StatusCodePagesOverrides",
            "Microsoft.AspNet.HealthChecks",
            "Microsoft.AspNet.Identity.EntityFramework6",
            "Microsoft.AspNet.Identity.Guid",
            "Microsoft.AspNet.Identity.Guid.pt-BR",
            "Microsoft.AspNet.Mvc-vHalfNext",
            "Microsoft.AspNet.Mvc.Futures-vHalfNext",
            "Microsoft.AspNet.OData.EntityBuilder",
            "Microsoft.AspNet.OData.Extensions.ODataQueryMapper",
            "Microsoft.AspNet.OData.Versioning.ApiExplorer",
            "Microsoft.AspNet.OData.Versioning",
            "Microsoft.AspNet.Razor-vHalfNext",
            "Microsoft.AspNet.SignalR.Client.Portable",
            "Microsoft.AspNet.SignalR.Ninject",
            "Microsoft.AspNet.WebApi.Extensions.Compression.Server",
            "Microsoft.AspNet.WebApi.Extensions.Compression.Server.Owin",
            "Microsoft.AspNet.WebApi.Extensions.Compression.Server.Owin.StrongName",
            "Microsoft.AspNet.WebApi.Extensions.Compression.Server.StrongName",
            "Microsoft.AspNet.WebApi.HelpPage.Ex",
            "Microsoft.AspNet.WebApi.LocalizedHelpPage",
            "Microsoft.AspNet.WebApi.MessageHandlers.Compression",
            "Microsoft.AspNet.WebApi.Versioning.ApiExplorer",
            "Microsoft.AspNet.WebApi.Versioning",
            "Microsoft.AspNet.WebApi.WebHost.zh-Hans1",
            "Microsoft.AspNet.WebPages-vHalfNext",
            "Microsoft.AspNetCore.ApiHelp.Core",
            "Microsoft.AspNetCore.ApiHelp",
            "Microsoft.AspNetCore.ApiWidgets",
            "Microsoft.AspNetCore.Authentication.ActiveDirectory",
            "Microsoft.AspNetCore.Authentication.LinkedIn",
            "Microsoft.AspNetCore.Authentication.QQ",
            "Microsoft.AspNetCore.Authentication.QQConnect",
            "Microsoft.AspNetCore.HealthChecks",
            "Microsoft.AspNetCore.Identity.MongoDB",
            "Microsoft.AspNetCore.JsonPatch.Net40",
            "Microsoft.AspNetCore.Mono",
            "Microsoft.AspNetCore.Mvc.Formatters.Xml.Extensions",
            "Microsoft.AspNetCore.Mvc.Versioning.ApiExplorer",
            "Microsoft.AspNetCore.Mvc.Versioning",
            "Microsoft.AspNetCore.OData",
            "Microsoft.AspNetCore.OData.IronBy",
            "Microsoft.AspNetCore.OData.LDC",
            "Microsoft.AspNetCore.OData.Radzen",
            "Microsoft.AspNetCore.OData.vNext",
            "Microsoft.AspNetCore.ReportService",
            "Microsoft.AspNetCore.Server.EmbedIO",
            "Microsoft.AspNetCore.Server.HttpListener",
            "Microsoft.AspNetCore.Server.SocketHttpListener",
            "Microsoft.AspNetCore.StaticFilesEx",
            "Microsoft.Data.Sqlite.Core.Backport",
            "Microsoft.Data.Sqlite.WinRT",
            "Microsoft.EntityFrameworkCore.AutoHistory",
            "Microsoft.EntityFrameworkCore.DynamicLinq",
            "Microsoft.EntityFrameworkCore.LazyLoading",
            "Microsoft.EntityFrameworkCore.UnitOfWork",
            "Microsoft.Extensions.Caching.Distributed.DynamoDb",
            "Microsoft.Extensions.Caching.Hekaton",
            "Microsoft.Extensions.Caching.Redis.Core",
            "Microsoft.Extensions.Caching.Redis.Jakeuj",
            "Microsoft.Extensions.Configuration.Contrib.GV.ConfigurationManager",
            "Microsoft.Extensions.Configuration.Contrib.Stormpath.EnvironmentVariables",
            "Microsoft.Extensions.Configuration.Contrib.Stormpath.ObjectReflection",
            "Microsoft.Extensions.Configuration.Contrib.Stormpath.PropertiesFile",
            "Microsoft.Extensions.Configuration.Contrib.Stormpath.Yaml",
            "Microsoft.Extensions.Configuration.DockerSecrets.Unofficial",
            "Microsoft.Extensions.Configuration.ImmutableBinder",
            "Microsoft.Extensions.Configuration.Placeholders",
            "Microsoft.Extensions.Configuration.Yaml",
            "Microsoft.Extensions.DependencyInjection.Scan",
            "Microsoft.Extensions.HealthChecks.AzureStorage",
            "Microsoft.Extensions.HealthChecks.SqlServer",
            "Microsoft.Extensions.Logging.Log4Net.AspNetCore",
            "Microsoft.Extensions.Logging.Log4Net.Jakeuj",
            "Microsoft.Extensions.Logging.Slack",
            "Microsoft.Extensions.ObjectPool.Net40",
            "Microsoft.Owin.Security.ApiKey",
            "Microsoft.Owin.Security.Authorization",
            "Microsoft.Owin.Security.Authorization.Mvc",
            "Microsoft.Owin.Security.Authorization.WebApi",
            "Microsoft.Owin.Security.Inovout.KuaJing",
            "Microsoft.Owin.Security.moin.Weixin",
            "Microsoft.Owin.Security.NengLongCookie",
            "Microsoft.Owin.Security.NengLongSSO",
            "Microsoft.Owin.Security.NengLongWeixin",
            "Microsoft.Owin.Security.NRII",
            "Microsoft.Owin.Security.OAuth.Manager.Client",
            "Microsoft.Owin.Security.OAuth.Manager.Server",
            "Microsoft.Owin.Security.QQ",
            "Microsoft.Owin.Security.Sina",
            "Microsoft.Owin.Security.Sina.Weibo",
            "Microsoft.Owin.Security.SinaWeibo",
            "Microsoft.Owin.Security.SSQSignon",
            "Microsoft.Owin.Security.Tencent",
            "Microsoft.Owin.Security.Tencent.QQ",
            "Microsoft.Owin.Security.Tencent.Wechat",
            "Microsoft.Owin.Security.WeChat",
            "Microsoft.Owin.Security.WeiBo",
            "Microsoft.Owin.Security.Youku",
            "Microsoft.VisualStudio.Azure.Fabric.MSBuild",
            "Microsoft.VisualStudio.CompilerModelHost",
            "Microsoft.VisualStudio.CompilerModelHost.netcore",
            "Microsoft.VisualStudio.Composition.AppHost",
            "Microsoft.VisualStudio.ConnectedServices",
            "Microsoft.VisualStudio.Debugger.Interop.Portable",
            "Microsoft.VisualStudio.DgmlTestModeling.2015.dll",
            "Microsoft.VisualStudio.DgmlTestModeling.dll",
            "Microsoft.VisualStudio.ExportTemplate",
            "Microsoft.VisualStudio.Jdt",
            "Microsoft.VisualStudio.LanguageServer.Client",
            "Microsoft.VisualStudio.LanguageServer.Protocol",
            "Microsoft.VisualStudio.Modeling.Sdk.11.0",
            "Microsoft.VisualStudio.Project.10.0",
            "Microsoft.VisualStudio.Project.11.0",
            "Microsoft.VisualStudio.Project.12.0",
            "Microsoft.VisualStudio.Project",
            "Microsoft.VisualStudio.QualityTools.LoadTestFramework",
            "Microsoft.VisualStudio.QualityTools.UnitTestFramework",
            "Microsoft.VisualStudio.QualityTools.UnitTestFramework.Updated",
            "Microsoft.VisualStudio.QualityTools.WebTestFramework",
            "Microsoft.VisualStudio.RemoteControl",
            "Microsoft.VisualStudio.RemoteControl.Net35",
            "Microsoft.VisualStudio.Services.NuGet.Bootstrap",
            "Microsoft.VisualStudio.Services.NuGet.CredentialProvider",
            "Microsoft.VisualStudio.Shell.15.0-pre",
            "Microsoft.VisualStudio.Shell.15.0",
            "Microsoft.VisualStudio.Shell.Framework-pre",
            "Microsoft.VisualStudio.SlowCheetah",
            "Microsoft.VisualStudio.SourceSafe.Interop",
            "Microsoft.VisualStudio.Telemetry",
            "Microsoft.VisualStudio.Telemetry.Net35",
            "Microsoft.VisualStudio.Telemetry.TestChannels",
            "Microsoft.VisualStudio.TestPlatform",
            "Microsoft.VisualStudio.TestPlatform.ObjectModel",
            "Microsoft.VisualStudio.TestWindow.Interfaces",
            "Microsoft.VisualStudio.Threading.Analyzers",
            "Microsoft.VisualStudio.Threading.DownlevelInstaller",
            "Microsoft.VisualStudio.UnitTesting",
            "Microsoft.VisualStudio.Utilities.Internal",
            "Microsoft.VisualStudio.Utilities.Internal.Net35",
            "Microsoft.VisualStudio.WizardFramework",
            "Microsoft.VisualStudio.Workspace",
            "Microsoft.VisualStudio.Workspace.VSIntegration",
            "Microsoft.VisualStudio.Workspaces",
        };

        public IEnumerable<PackageVerifierIssue> Validate(PackageAnalysisContext context)
        {
            if (ExternallyOwnedPackageIds.Contains(context.Metadata.Id, StringComparer.OrdinalIgnoreCase))
            {
                yield return PackageIssueFactory.IdIsNotOwned(context.Metadata.Id);
            }

            if (OwnedPackageIds.Contains(context.Metadata.Id, StringComparer.OrdinalIgnoreCase))
            {
                yield break;
            }

            if (OwnedPrefixes.Any(prefix => context.Metadata.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                yield break;
            }
        }
    }
}
