using System;
using System.Threading.Tasks;
using FractalCode.Core;
using FractalCode.Contracts;
using FractalCode.Analysis;
using FractalCode.Context;
using FractalCode.Impact;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using YamlDotNet.Serialization;
using FractalCode.Monitoring;
using FractalCode.Patterns;

namespace FractalCode.CLI
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Setup dependency injection
            var services = ConfigureServices();

            // Create and configure root command
            var rootCommand = new RootCommand(
                "FractalCode CLI - Code Fractalization Protocol Implementation",
                services,
                services.GetRequiredService<ILogger<RootCommand>>()
            );

            // Add subcommands
            rootCommand.AddCommand(ConfigureFractalCommands(services));
            rootCommand.AddCommand(ConfigureContractCommands(services));
            rootCommand.AddCommand(ConfigureAnalysisCommands(services));
            rootCommand.AddCommand(ConfigureContextCommands(services));
            rootCommand.AddCommand(ConfigureImpactCommands(services));

            return await rootCommand.ExecuteAsync(args);
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });

            // Add core services
            services.AddSingleton<IFractalManager, FractalManager>();
            services.AddSingleton<IContractValidator, ContractValidator>();
            services.AddSingleton<ICodeAnalyzer, CodeAnalyzer>();
            services.AddSingleton<IContextGenerator, ContextGenerator>();
            services.AddSingleton<IImpactAnalyzer, ImpactAnalyzer>();
            services.AddSingleton<IYamlProcessor, YamlProcessor>();

            // Add specialized services
            services.AddSingleton<IPatternRegistry, PatternRegistry>();
            services.AddSingleton<IResourceManager, ResourceManager>();
            services.AddSingleton<IHealthMonitor, HealthMonitor>();

            return services.BuildServiceProvider();
        }

        private static Command ConfigureFractalCommands(IServiceProvider services)
        {
            var fractalCommand = new Command("fractal", "Manage fractal components");

            // fractal init command
            var initCommand = new Command("init", "Initialize a new fractal component");
            initCommand.AddArgument(new Argument("name", "Name of the fractal", true, null, typeof(string)));
            initCommand.AddOption(new Option("--parent", "Parent fractal name", false, null, typeof(string)));
            initCommand.AddOption(new Option("--template", "Template to use for initialization", false, null, typeof(string)));

            initCommand.Handler = CommandHandler.Create<string, string, string>(
                async (name, parent, template) =>
                {
                    try
                    {
                        var fractalManager = services.GetRequiredService<IFractalManager>();
                        var options = new FractalInitOptions
                        {
                            Name = name,
                            ParentName = parent,
                            Template = template
                        };

                        await fractalManager.InitializeFractalAsync(options);
                        Console.WriteLine($"Successfully initialized fractal: {name}");
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error initializing fractal: {ex.Message}");
                        return 1;
                    }
                }, services);

            fractalCommand.AddCommand(initCommand);
            return fractalCommand;
        }

        private static Command ConfigureContractCommands(IServiceProvider services)
        {
            var contractCommand = new Command("contract", "Manage fractal contracts");

            // contract validate command
            var validateCommand = new Command("validate", "Validate fractal contracts");
            validateCommand.AddArgument(new Argument("path", "Path to contract file or directory", true, null, typeof(string)));

            validateCommand.Handler = CommandHandler.Create<string, IContractValidator>(
                async (path, validator) =>
                {
                    try
                    {
                        var result = await validator.ValidateContractsAsync(path);
                        if (result.IsValid)
                        {
                            Console.WriteLine("Contract validation successful");
                            return 0;
                        }
                        else
                        {
                            Console.Error.WriteLine("Contract validation failed:");
                            foreach (var error in result.Errors)
                            {
                                Console.Error.WriteLine($"- {error}");
                            }
                            return 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error validating contracts: {ex.Message}");
                        return 1;
                    }
                }, services);

            contractCommand.AddCommand(validateCommand);
            return contractCommand;
        }

        private static Command ConfigureAnalysisCommands(IServiceProvider services)
        {
            var analysisCommand = new Command("analyze", "Analyze codebase");

            // analyze command
            analysisCommand.AddArgument(new Argument("path", "Path to analyze", true, null, typeof(string)));
            analysisCommand.AddOption(new Option("--output", "Output file path", false, null, typeof(string)));

            analysisCommand.Handler = CommandHandler.Create<string, string, ICodeAnalyzer>(
                async (path, output, analyzer) =>
                {
                    try
                    {
                        var analysis = await analyzer.AnalyzeCodebaseAsync(path);
                        if (!string.IsNullOrEmpty(output))
                        {
                            await analyzer.SaveAnalysisResultsAsync(analysis, output);
                            Console.WriteLine($"Analysis results saved to: {output}");
                        }
                        else
                        {
                            Console.WriteLine(analysis.ToString());
                        }
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error analyzing codebase: {ex.Message}");
                        return 1;
                    }
                }, services);

            return analysisCommand;
        }

        private static Command ConfigureContextCommands(IServiceProvider services)
        {
            var contextCommand = new Command("context", "Manage fractal context");

            // context generate command
            var generateCommand = new Command("generate", "Generate context for AI processing");
            generateCommand.AddArgument(new Argument("path", "Path to generate context for", true, null, typeof(string)));
            generateCommand.AddOption(new Option("--for-ai", "Optimize output for AI consumption", false, false, typeof(bool)));

            generateCommand.Handler = CommandHandler.Create<string, bool, IContextGenerator>(
                async (path, forAi, generator) =>
                {
                    try
                    {
                        var context = await generator.GenerateContextAsync(path, forAi);
                        Console.WriteLine(context);
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error generating context: {ex.Message}");
                        return 1;
                    }
                }, services);

            contextCommand.AddCommand(generateCommand);
            return contextCommand;
        }

        private static Command ConfigureImpactCommands(IServiceProvider services)
        {
            var impactCommand = new Command("impact", "Analyze change impact");

            // impact analysis command
            impactCommand.AddOption(new Option("--change", "Path to change specification file", true, null, typeof(string)));

            impactCommand.Handler = CommandHandler.Create<string, IImpactAnalyzer>(
                async (change, analyzer) =>
                {
                    try
                    {
                        var analysis = await analyzer.AnalyzeChangeImpactAsync(change);
                        Console.WriteLine("Impact Analysis Results:");
                        Console.WriteLine(analysis.ToString());
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error analyzing impact: {ex.Message}");
                        return 1;
                    }
                }, services);

            return impactCommand;
        }
    }
}