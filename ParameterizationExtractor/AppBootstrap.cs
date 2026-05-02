using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParameterizationExtractor.Logic.MSSQL;
using Quipu.ParameterizationExtractor.Common;
using Quipu.ParameterizationExtractor.Configs;
using Quipu.ParameterizationExtractor.DSL.Connector;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.MSSQL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Quipu.ParameterizationExtractor
{
    public class AppArgs : IAppArgs
    {
        public AppArgs()
        {

        }

        [Option('d', "database")]
        public string DBName { get; set; }

        [Option('s', "serverName")]
        public string ServerName { get; set; }

        [Option('p', "package", Required = true, HelpText = "Path to package")]
        public string PathToPackage { get; set; }

        [Option('n', "connectionName", Default = "SourceDB")]
        public string ConnectionName { get; set; }

        [Option('o', "outputFolder", Default = "Output")]
        public string OutputFolder { get; set; }

        [Option('i', "Interactive", Default = false)]
        public bool Interactive { get; set; }

        public static IAppArgs GetAppArgs(string[] args)
        {
            var result = Parser.Default.ParseArguments<AppArgs>(args);
            if (result is not Parsed<AppArgs> parsed)
                throw new Exception("Argument parsing failed.");

            var a = parsed.Value;

            // Pinned cross-arg validation (messages preserved as-is from the FCLP version).
            // The messages are flipped — they name the arg the user *did* specify rather than
            // the missing one. Fixing the wording is a separate follow-up after this feature.
            if (string.IsNullOrEmpty(a.ServerName) && !string.IsNullOrEmpty(a.DBName))
                throw new Exception("Please specify DBName!");

            if (string.IsNullOrEmpty(a.DBName) && !string.IsNullOrEmpty(a.ServerName))
                throw new Exception("Please specify ServerName!");

            return a;
        }
    }

    public static class AppBootstrap
    {
        public static IAppBuilder CreateAppBuilder(string[] args)
        {
            var a = AppArgs.GetAppArgs(args);
            var ser = new ConfigSerializer(new FparsecConnector());

            return new AppBuilder(a).ConfigureServices(_ =>_
                                                            .AddSingleton<IFileService, FileService>()
                                                            .AddSingleton<IExtractConfiguration>(ser.GetGlobalConfig())
                                                            .AddSingleton<ICanSerializeConfigs, ConfigSerializer>()
                                                            .AddSingleton<PackageProcessor>()
                                                            .AddSingleton<IDSLConnector, FparsecConnector>()
                                                            ); 
        }
    }

    public static class DI
    {

        public static IAppBuilder AddExecutor(this IAppBuilder appBuilder)
        {
            return appBuilder.ConfigureServices((services, args) =>
            {
                if (args.Interactive)
                    services.AddSingleton<IExecutor, DSLExecutor>();
                else
                    services.AddSingleton<IExecutor, FromFileExecutor>();
            });                       
        }

        public static IAppBuilder AddMSSQL(this IAppBuilder appBuilder)
        {
            return appBuilder.ConfigureServices(_ => _.AddTransient<ISqlBuilder, MSSqlBuilder>() //must be transient, MSSqlBuilder is no thread safe
                                                     .AddSingleton<ISourceSchema, MSSQLSourceSchema>()
                                                     .AddSingleton<IObjectMetaDataProvider, ObjectMetaDataProvider>()
                                                     .AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>()
                                                     .AddTransient<IDependencyBuilder, DependencyBuilder>() //must be transient, DependencyBuilder is no thread safe
                                                     .AddTransient<IMetaDataInitializer, MetaDataInitializer>()
                                                     .AddSingleton<IConnectionStringResolver, ConnectionStringResolver>()
                                                );
        }
       
    }
   
}
