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
        private readonly CommandOption _source;
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

        public Program(
            CommandLineApplication app,
            CommandOption azureStorageConnectionString,
            CommandOption azureStorageContainer,
            CommandOption name,
            CommandOption source,
            CommandOption quiet)
        {
            _app = app;
            _source = source;
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

            var source = app.Option(
                "--source <DIRS>",
                "Path to the file to be uploaded",
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
                connectionString,
                container,
                name,
                source,
                quiet);

            app.OnExecute(new Func<int>(program.Execute));

            return app.Execute(args);
        }

        private int Execute()
        {
            try
            {
                if (!_source.HasValue() ||
                    !_connectionString.HasValue() ||
                    !_container.HasValue())
                {
                    _app.ShowHelp();
                    return 1;
                }

                if (!File.Exists(_source.Value()))
                {
                    Logger.LogError("Unable to find package cache file {PackageCacheFileName}.");
                    return 1;
                }

                var account = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(_connectionString.Value());
                var client = account.CreateCloudBlobClient();
                var container = client.GetContainerReference(_container.Value());

                container.CreateIfNotExistsAsync().Wait();

                var blob = container.GetBlockBlobReference(_name.HasValue() ? _name.Value() : Path.GetFileName(_source.Value()));
                blob.UploadFromFileAsync(_source.Value()).Wait();

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
