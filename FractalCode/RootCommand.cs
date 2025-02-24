using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FractalCode.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FractalCode.CLI
{
    /// <summary>
    /// Represents the root command for the FractalCode CLI tool
    /// </summary>
    public class RootCommand : Command
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RootCommand> _logger;
        private readonly string _version;

        public RootCommand(
            string description,
            IServiceProvider serviceProvider,
            ILogger<RootCommand> logger,
            string version = "1.0.0") : base("fractalcode", description)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _version = version;

            InitializeRootCommand();
        }

        private void InitializeRootCommand()
        {
            // Add common options
            AddOption(new Option("--verbose", "Enable verbose logging", false, false, typeof(bool)));
            AddOption(new Option("--config", "Path to configuration file", false, null, typeof(string)));
            AddOption(new Option("--version", "Display version information", false, false, typeof(bool)));

            // Set root command handler
            Handler = CommandHandler.Create<bool, string, bool>(HandleRootCommandAsync, _serviceProvider);
        }

        public async Task<int> ExecuteAsync(string[] args)
        {
            try
            {
                // Parse and validate arguments
                var parsedArgs = ParseArguments(args);

                // Check for version flag
                if (parsedArgs.ContainsKey("--version") && (bool)parsedArgs["--version"])
                {
                    Console.WriteLine($"FractalCode CLI version {_version}");
                    return 0;
                }

                // Configure verbose logging if requested
                if (parsedArgs.ContainsKey("--verbose") && (bool)parsedArgs["--verbose"])
                {
                    var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
                    loggerFactory.AddProvider(new ConsoleLoggerProvider(LogLevel.Debug));
                }

                // Load configuration if specified
                if (parsedArgs.ContainsKey("--config"))
                {
                    var configPath = parsedArgs["--config"]?.ToString();
                    if (!string.IsNullOrEmpty(configPath))
                    {
                        await LoadConfigurationAsync(configPath);
                    }
                }

                // If there are subcommands, find and execute the appropriate one
                if (args.Length > 0 && !args[0].StartsWith("--"))
                {
                    var subcommand = Subcommands.FirstOrDefault(c => c.Name == args[0]);
                    if (subcommand != null)
                    {
                        return await subcommand.ExecuteAsync(args.Skip(1).ToArray());
                    }
                }

                // If no subcommand specified or not found, show help
                Console.WriteLine(GetHelpText());
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command");
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private Dictionary<string, object> ParseArguments(string[] args)
        {
            var parsed = new Dictionary<string, object>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    var option = Options.FirstOrDefault(o => $"--{o.Name}" == args[i]);
                    if (option != null)
                    {
                        if (option.OptionType == typeof(bool))
                        {
                            parsed[args[i]] = true;
                        }
                        else if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        {
                            parsed[args[i]] = args[++i];
                        }
                    }
                }
            }

            return parsed;
        }

        private async Task LoadConfigurationAsync(string configPath)
        {
            try
            {
                var yamlProcessor = _serviceProvider.GetRequiredService<IYamlProcessor>();
                var config = await yamlProcessor.DeserializeAsync<Dictionary<string, object>>(configPath);

                // Apply configuration
                ApplyConfiguration(config);

                _logger.LogInformation($"Loaded configuration from {configPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading configuration from {configPath}");
                throw new InvalidOperationException($"Failed to load configuration: {ex.Message}", ex);
            }
        }

        private void ApplyConfiguration(Dictionary<string, object> config)
        {
            if (config == null) return;

            // Apply relevant configuration settings
            if (config.TryGetValue("logging", out var loggingConfig))
            {
                ConfigureLogging(loggingConfig as Dictionary<string, object>);
            }

            if (config.TryGetValue("thresholds", out var thresholdConfig))
            {
                ConfigureThresholds(thresholdConfig as Dictionary<string, object>);
            }

            // Add more configuration sections as needed
        }

        private void ConfigureLogging(Dictionary<string, object> loggingConfig)
        {
            if (loggingConfig == null) return;

            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

            if (loggingConfig.TryGetValue("level", out var levelObj) &&
                Enum.TryParse<LogLevel>(levelObj?.ToString(), true, out var level))
            {
                loggerFactory.AddProvider(new ConsoleLoggerProvider(level));
            }
        }

        private void ConfigureThresholds(Dictionary<string, object> thresholdConfig)
        {
            if (thresholdConfig == null) return;

            // Configure system thresholds based on configuration
            foreach (var (key, value) in thresholdConfig)
            {
                if (double.TryParse(value?.ToString(), out var threshold))
                {
                    _logger.LogDebug($"Setting threshold {key} to {threshold}");
                    // Apply threshold to relevant system component
                }
            }
        }

        /// <summary>
        /// Adds multiple commands to the root command
        /// </summary>
        public void AddCommands(params Command[] commands)
        {
            if (commands == null) return;

            foreach (var command in commands.Where(c => c != null))
            {
                AddCommand(command);
            }
        }
        private async Task<int> HandleRootCommandAsync(bool verbose, string config, bool version)
        {
            Console.WriteLine(GetHelpText());
            return await Task.FromResult(0);
        }
        public override string GetHelpText()
        {
            var help = new List<string>
            {
                $"FractalCode CLI v{_version}",
                base.GetHelpText(),
                "",
                "Global Options:",
                "  --verbose     Enable verbose logging",
                "  --config      Specify configuration file path",
                "  --version     Display version information",
                "",
                "For more information about a command, use: fractalcode <command> --help"
            };

            return string.Join(Environment.NewLine, help);
        }
    }

    /// <summary>
    /// Basic console logger provider for verbose logging
    /// </summary>
    internal class ConsoleLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel _minLevel;

        public ConsoleLoggerProvider(LogLevel minLevel)
        {
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleLogger(categoryName, _minLevel);
        }

        public void Dispose() { }
    }

    internal class ConsoleLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogLevel _minLevel;

        public ConsoleLogger(string categoryName, LogLevel minLevel)
        {
            _categoryName = categoryName;
            _minLevel = minLevel;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var color = Console.ForegroundColor;

            Console.ForegroundColor = GetLogLevelColor(logLevel);
            Console.Error.WriteLine($"[{logLevel}] {_categoryName}: {message}");

            if (exception != null)
            {
                Console.Error.WriteLine(exception.ToString());
            }

            Console.ForegroundColor = color;
        }

        private static ConsoleColor GetLogLevelColor(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Critical => ConsoleColor.Red,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Information => ConsoleColor.Green,
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Trace => ConsoleColor.Gray,
                _ => ConsoleColor.Gray
            };
        }
    }
}