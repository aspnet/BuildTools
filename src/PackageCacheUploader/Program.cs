// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace DependenciesPackager
{
    class Program
    {
        private ILogger _logger;
        private CommandLineApplication _app;
        private readonly CommandOption _sourceFolder;
        private readonly CommandOption _version;
        private readonly CommandOption _prefix;
        private readonly CommandOption _connectionString;
        private readonly CommandOption _container;
        private readonly CommandOption _name;
        private readonly CommandOption _quiet;

        public ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    var loggerFactory = new LoggerFactory();
                    var logLevel = _quiet.HasValue() ? LogLevel.Warning : LogLevel.Information;

                    loggerFactory.AddConsole(logLevel, includeScopes: false);

                    _logger = loggerFactory.CreateLogger<Program>();
                }

                return _logger;
            }
        }

        private string PackageCacheFileName => $"{_prefix.Value()}.{_version.Value()}.packagecache.zip";

        public Program(
            CommandLineApplication app,
            CommandOption prefix,
            CommandOption version,
            CommandOption azureStorageConnectionString,
            CommandOption azureStorageContainer,
            CommandOption name,
            CommandOption sourceFolder,
            CommandOption quiet)
        {
            _app = app;
            _sourceFolder = sourceFolder;
            _version = version;
            _prefix = prefix;
            _connectionString = azureStorageConnectionString;
            _container = azureStorageContainer;
            _name = name;
            _quiet = quiet;
        }

        static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "DependenciesPackager";

            app.HelpOption("-?|-h|--help");

            var sourceFolder = app.Option(
                "--source <DIRS>",
                "Path to the directory containing the packages cache zip file",
                CommandOptionType.SingleValue);

            var version = app.Option(
                "--version <VERSION>",
                "The version of the artifacts produced",
                CommandOptionType.SingleValue);

            var prefix = app.Option(
                "--prefix",
                "The prefix to use for the zip file name",
                CommandOptionType.SingleValue);

            var connectionString = app.Option(
                "--connection",
                "Azure torage connection string",
                CommandOptionType.SingleValue);

            var container = app.Option(
                "--container <NAME>",
                "The name for the container to store the package cache zip file.",
                CommandOptionType.SingleValue);

            var quiet = app.Option(
                "--quiet",
                "Avoids printing to the output anything other than warnings or errors",
                CommandOptionType.NoValue);

            var name = app.Option(
                "--name",
                "The change the file name to the given string on Azure Blob Storage. If missing, use the original file name.",
                CommandOptionType.SingleValue);

            var program = new Program(
                app,
                prefix,
                version,
                connectionString,
                container,
                name,
                sourceFolder,
                quiet);

            app.OnExecute(new Func<int>(program.Execute));

            return app.Execute(args);
        }

        private int Execute()
        {
            try
            {
                if (!_sourceFolder.HasValue() ||
                    !_connectionString.HasValue() ||
                    !_container.HasValue() ||
                    !_prefix.HasValue() ||
                    !_version.HasValue())
                {
                    _app.ShowHelp();
                    return 1;
                }

                var fullpath = Path.Combine(_sourceFolder.Value(), PackageCacheFileName);
                if (!File.Exists(fullpath))
                {
                    Logger.LogError("Unable to find package cache file： {PackageCacheFileName}.");
                    return 1;
                }

                var account = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(_connectionString.Value());
                var client = account.CreateCloudBlobClient();
                var container = client.GetContainerReference(_container.Value());

                container.CreateIfNotExistsAsync().Wait();

                var blob = container.GetBlockBlobReference(_name.HasValue() ? _name.Value() : PackageCacheFileName);
                blob.UploadFromFileAsync(fullpath).Wait();

                return 0;
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
                _app.ShowHelp();
                return 1;
            }
        }
    }
}
