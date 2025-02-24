using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using FractalCode.Contracts;
using FractalCode.Core.Models;

namespace FractalCode.Core
{
    public class FractalInitOptions
    {
        public string Name { get; set; }
        public string ParentName { get; set; }
        public string Template { get; set; }
    }

    public interface IFractalManager
    {
        Task<Fractal> InitializeFractalAsync(FractalInitOptions options);
        Task<Fractal> GetFractalAsync(string name);
        Task<bool> ValidateFractalAsync(string name);
        Task<bool> UpdateFractalAsync(string name, Fractal fractal);
    }

    public class FractalManager : IFractalManager
    {
        private readonly ILogger<FractalManager> _logger;
        private readonly IContractValidator _contractValidator;
        private readonly IYamlProcessor _yamlProcessor;
        private readonly string _baseDirectory;

        public FractalManager(
            ILogger<FractalManager> logger,
            IContractValidator contractValidator,
            IYamlProcessor yamlProcessor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _contractValidator = contractValidator ?? throw new ArgumentNullException(nameof(contractValidator));
            _yamlProcessor = yamlProcessor ?? throw new ArgumentNullException(nameof(yamlProcessor));
            _baseDirectory = Directory.GetCurrentDirectory();
        }

        public async Task<Fractal> InitializeFractalAsync(FractalInitOptions options)
        {
            try
            {
                _logger.LogInformation($"Initializing fractal: {options.Name}");

                // Validate input
                if (string.IsNullOrWhiteSpace(options.Name))
                {
                    throw new ArgumentException("Fractal name is required");
                }

                // Create fractal structure
                var fractal = new Fractal
                {
                    Name = options.Name,
                    ParentName = options.ParentName,
                    Template = options.Template ?? "default",
                    CreatedAt = DateTime.UtcNow
                };

                // Create directory structure
                var fractalPath = CreateFractalDirectoryStructure(fractal);

                // Initialize template
                await InitializeTemplateAsync(fractal, fractalPath);

                // Create initial contracts
                await CreateInitialContractsAsync(fractal, fractalPath);

                // Create context documentation
                await CreateContextDocumentationAsync(fractal, fractalPath);

                _logger.LogInformation($"Successfully initialized fractal: {options.Name}");

                return fractal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize fractal: {options.Name}");
                throw;
            }
        }

        public async Task<Fractal> GetFractalAsync(string name)
        {
            try
            {
                var fractalPath = Path.Combine(_baseDirectory, name);
                if (!Directory.Exists(fractalPath))
                {
                    throw new DirectoryNotFoundException($"Fractal not found: {name}");
                }

                // Load fractal configuration
                var configPath = Path.Combine(fractalPath, "data", "config.yaml");
                var fractal = await _yamlProcessor.DeserializeAsync<Fractal>(configPath);

                return fractal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get fractal: {name}");
                throw;
            }
        }

        public async Task<bool> ValidateFractalAsync(string name)
        {
            try
            {
                var fractal = await GetFractalAsync(name);
                var fractalPath = Path.Combine(_baseDirectory, name);

                // Validate structure
                if (!ValidateFractalStructure(fractalPath))
                {
                    return false;
                }

                // Validate contracts
                var contractsPath = Path.Combine(fractalPath, "knowledge", "contracts");
                var contractValidation = await _contractValidator.ValidateContractsAsync(contractsPath);
                if (!contractValidation.IsValid)
                {
                    _logger.LogWarning("Contract validation failed");
                    return false;
                }

                // Validate context
                if (!await ValidateContextAsync(fractalPath))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to validate fractal: {name}");
                throw;
            }
        }

        public async Task<bool> UpdateFractalAsync(string name, Fractal fractal)
        {
            try
            {
                var fractalPath = Path.Combine(_baseDirectory, name);
                if (!Directory.Exists(fractalPath))
                {
                    throw new DirectoryNotFoundException($"Fractal not found: {name}");
                }

                // Update configuration
                var configPath = Path.Combine(fractalPath, "data", "config.yaml");
                await _yamlProcessor.SerializeAsync(fractal, configPath);

                // Validate after update
                return await ValidateFractalAsync(name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update fractal: {name}");
                throw;
            }
        }

        private string CreateFractalDirectoryStructure(Fractal fractal)
        {
            var fractalPath = Path.Combine(_baseDirectory, fractal.Name);

            // Create main directories
            Directory.CreateDirectory(Path.Combine(fractalPath, "implementation", "src"));
            Directory.CreateDirectory(Path.Combine(fractalPath, "implementation", "tests"));
            Directory.CreateDirectory(Path.Combine(fractalPath, "data", "resources"));
            Directory.CreateDirectory(Path.Combine(fractalPath, "knowledge", "contracts"));

            return fractalPath;
        }

        private async Task InitializeTemplateAsync(Fractal fractal, string fractalPath)
        {
            var templatePath = Path.Combine("templates", fractal.Template);
            if (!Directory.Exists(templatePath))
            {
                _logger.LogWarning($"Template not found: {fractal.Template}, using default");
                templatePath = Path.Combine("templates", "default");
            }

            // Copy template files
            await CopyTemplateFilesAsync(templatePath, fractalPath);
        }

        private async Task CreateInitialContractsAsync(Fractal fractal, string fractalPath)
        {
            var contracts = new Dictionary<string, object>
            {
                { "interface.yaml", CreateDefaultInterfaceContract(fractal) },
                { "behavior.yaml", CreateDefaultBehaviorContract(fractal) },
                { "resource.yaml", CreateDefaultResourceContract(fractal) }
            };

            var contractsPath = Path.Combine(fractalPath, "knowledge", "contracts");
            foreach (var contract in contracts)
            {
                await _yamlProcessor.SerializeAsync(
                    contract.Value,
                    Path.Combine(contractsPath, contract.Key)
                );
            }
        }

        private async Task CreateContextDocumentationAsync(Fractal fractal, string fractalPath)
        {
            var context = new
            {
                fractal.Name,
                fractal.ParentName,
                CreatedAt = DateTime.UtcNow,
                Decisions = new List<object>(),
                Dependencies = new List<object>(),
                Evolution = new List<object>()
            };

            await _yamlProcessor.SerializeAsync(
                context,
                Path.Combine(fractalPath, "knowledge", "context.yaml")
            );
        }

        private async Task<bool> ValidateContextAsync(string fractalPath)
        {
            try
            {
                var contextPath = Path.Combine(fractalPath, "knowledge", "context.yaml");
                if (!File.Exists(contextPath))
                {
                    _logger.LogWarning("Context documentation not found");
                    return false;
                }

                // Validate context structure
                var context = await _yamlProcessor.DeserializeAsync<dynamic>(contextPath);

                // Add specific context validation rules here
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Context validation failed");
                return false;
            }
        }

        private bool ValidateFractalStructure(string fractalPath)
        {
            // Verify required directories exist
            var requiredPaths = new[]
            {
                Path.Combine(fractalPath, "implementation", "src"),
                Path.Combine(fractalPath, "implementation", "tests"),
                Path.Combine(fractalPath, "data", "resources"),
                Path.Combine(fractalPath, "knowledge", "contracts")
            };

            foreach (var path in requiredPaths)
            {
                if (!Directory.Exists(path))
                {
                    _logger.LogWarning($"Required directory not found: {path}");
                    return false;
                }
            }

            return true;
        }

        private async Task CopyTemplateFilesAsync(string templatePath, string fractalPath)
        {
            foreach (var file in Directory.GetFiles(templatePath, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(templatePath, file);
                var targetPath = Path.Combine(fractalPath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                File.Copy(file, targetPath);
            }
        }

        private object CreateDefaultInterfaceContract(Fractal fractal)
        {
            return new
            {
                Name = fractal.Name,
                Version = "1.0.0",
                Inputs = new List<object>(),
                Outputs = new List<object>(),
                ExtensionPoints = new List<string>()
            };
        }

        private object CreateDefaultBehaviorContract(Fractal fractal)
        {
            return new
            {
                Name = fractal.Name,
                Version = "1.0.0",
                Operations = new List<object>(),
                ConcurrencyRules = new List<object>(),
                PerformanceConstraints = new List<object>()
            };
        }

        private object CreateDefaultResourceContract(Fractal fractal)
        {
            return new
            {
                Name = fractal.Name,
                Version = "1.0.0",
                ResourceRequirements = new List<object>(),
                AccessPatterns = new List<object>(),
                ScalingRules = new List<object>()
            };
        }
    }
}