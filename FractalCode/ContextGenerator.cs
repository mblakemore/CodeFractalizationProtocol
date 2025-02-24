using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FractalCode.Core;
using FractalCode.Contracts;
using Newtonsoft.Json;
using System.Xml;

namespace FractalCode.Context
{
    public interface IContextGenerator
    {
        Task<string> GenerateContextAsync(string path, bool forAi = false);
        Task<string> GenerateDecisionHistoryAsync(string path);
        Task<string> GenerateDependencyContextAsync(string path);
    }

    public class ContextGenerator : IContextGenerator
    {
        private readonly ILogger<ContextGenerator> _logger;
        private readonly IFractalManager _fractalManager;
        private readonly IContractValidator _contractValidator;
        private readonly IDeserializer _yamlDeserializer;
        private readonly ISerializer _yamlSerializer;

        public ContextGenerator(
            ILogger<ContextGenerator> logger,
            IFractalManager fractalManager,
            IContractValidator contractValidator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fractalManager = fractalManager ?? throw new ArgumentNullException(nameof(fractalManager));
            _contractValidator = contractValidator ?? throw new ArgumentNullException(nameof(contractValidator));

            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public async Task<string> GenerateContextAsync(string path, bool forAi = false)
        {
            try
            {
                _logger.LogInformation($"Generating context for path: {path}");

                // Validate path
                if (!Directory.Exists(path))
                {
                    throw new DirectoryNotFoundException($"Directory not found: {path}");
                }

                // Collect context information
                var context = new Dictionary<string, object>
                {
                    ["component"] = await GetComponentContextAsync(path),
                    ["contracts"] = await GetContractContextAsync(path),
                    ["decisions"] = await GetDecisionHistoryAsync(path),
                    ["dependencies"] = await GetDependencyContextAsync(path),
                    ["patterns"] = await GetPatternContextAsync(path)
                };

                // Format output based on target
                if (forAi)
                {
                    return FormatForAI(context);
                }
                else
                {
                    return _yamlSerializer.Serialize(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating context for {path}");
                throw;
            }
        }

        public async Task<string> GenerateDecisionHistoryAsync(string path)
        {
            try
            {
                _logger.LogInformation($"Generating decision history for path: {path}");

                var decisions = await GetDecisionHistoryAsync(path);
                return _yamlSerializer.Serialize(new { decisions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating decision history for {path}");
                throw;
            }
        }

        public async Task<string> GenerateDependencyContextAsync(string path)
        {
            try
            {
                _logger.LogInformation($"Generating dependency context for path: {path}");

                var dependencies = await GetDependencyContextAsync(path);
                return _yamlSerializer.Serialize(new { dependencies });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating dependency context for {path}");
                throw;
            }
        }

        private async Task<Dictionary<string, object>> GetComponentContextAsync(string path)
        {
            // Load fractal configuration
            var configPath = Path.Combine(path, "data", "config.yaml");
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Configuration not found: {configPath}");
            }

            var configContent = await File.ReadAllTextAsync(configPath);
            var config = _yamlDeserializer.Deserialize<Dictionary<string, object>>(configContent);

            // Enhance with implementation details
            var srcPath = Path.Combine(path, "implementation", "src");
            var sourceFiles = Directory.Exists(srcPath)
                ? Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>();

            return new Dictionary<string, object>
            {
                ["name"] = config["name"],
                ["version"] = config["version"],
                ["sourceFiles"] = sourceFiles.Select(Path.GetFileName).ToList(),
                ["responsibilities"] = await GetResponsibilitiesAsync(path)
            };
        }

        private async Task<Dictionary<string, object>> GetContractContextAsync(string path)
        {
            var contractsPath = Path.Combine(path, "knowledge", "contracts");
            if (!Directory.Exists(contractsPath))
            {
                return new Dictionary<string, object>();
            }

            var contracts = new Dictionary<string, object>();
            foreach (var contractFile in Directory.GetFiles(contractsPath, "*.yaml"))
            {
                var content = await File.ReadAllTextAsync(contractFile);
                var contract = _yamlDeserializer.Deserialize<Dictionary<string, object>>(content);
                contracts[Path.GetFileNameWithoutExtension(contractFile)] = contract;
            }

            return contracts;
        }

        private async Task<List<object>> GetDecisionHistoryAsync(string path)
        {
            var contextPath = Path.Combine(path, "knowledge", "context.yaml");
            if (!File.Exists(contextPath))
            {
                return new List<object>();
            }

            var content = await File.ReadAllTextAsync(contextPath);
            var context = _yamlDeserializer.Deserialize<Dictionary<string, object>>(content);

            return (context.ContainsKey("decisions") && context["decisions"] is List<object> decisions)
                ? decisions
                : new List<object>();
        }

        private async Task<List<object>> GetDependencyContextAsync(string path)
        {
            var dependencies = new List<object>();

            // Check contract dependencies
            var contracts = await GetContractContextAsync(path);
            foreach (var contract in contracts)
            {
                if (contract.Value is Dictionary<string, object> contractDict &&
                    contractDict.ContainsKey("dependencies"))
                {
                    dependencies.AddRange((List<object>)contractDict["dependencies"]);
                }
            }

            // Check implementation dependencies
            var srcPath = Path.Combine(path, "implementation", "src");
            if (Directory.Exists(srcPath))
            {
                foreach (var file in Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories))
                {
                    var content = await File.ReadAllTextAsync(file);
                    var refs = ExtractReferences(content);
                    dependencies.AddRange(refs);
                }
            }

            return dependencies.Distinct().ToList();
        }

        private async Task<List<object>> GetPatternContextAsync(string path)
        {
            var patternsPath = Path.Combine(path, "knowledge", "patterns.yaml");
            if (!File.Exists(patternsPath))
            {
                return new List<object>();
            }

            var content = await File.ReadAllTextAsync(patternsPath);
            var patterns = _yamlDeserializer.Deserialize<Dictionary<string, object>>(content);

            return (patterns.ContainsKey("patterns") && patterns["patterns"] is List<object> patternList)
                ? patternList
                : new List<object>();
        }

        private async Task<List<string>> GetResponsibilitiesAsync(string path)
        {
            var responsibilities = new List<string>();

            // Extract from contracts
            var contracts = await GetContractContextAsync(path);
            foreach (var contract in contracts.Values)
            {
                if (contract is Dictionary<string, object> contractDict &&
                    contractDict.ContainsKey("responsibilities"))
                {
                    responsibilities.AddRange(((List<object>)contractDict["responsibilities"])
                        .Select(r => r.ToString()));
                }
            }

            // Extract from implementation
            var srcPath = Path.Combine(path, "implementation", "src");
            if (Directory.Exists(srcPath))
            {
                foreach (var file in Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories))
                {
                    var content = await File.ReadAllTextAsync(file);
                    responsibilities.AddRange(ExtractResponsibilities(content));
                }
            }

            return responsibilities.Distinct().ToList();
        }

        private string FormatForAI(Dictionary<string, object> context)
        {
            // Create AI-optimized format
            var aiContext = new Dictionary<string, object>
            {
                ["component"] = context["component"],
                ["contracts"] = SimplifyContracts(context["contracts"]),
                ["decisions"] = SimplifyDecisions(context["decisions"]),
                ["dependencies"] = SimplifyDependencies(context["dependencies"]),
                ["patterns"] = SimplifyPatterns(context["patterns"])
            };

            // Convert to JSON for consistent parsing
            return JsonConvert.SerializeObject(aiContext, Newtonsoft.Json.Formatting.Indented);
        }

        private object SimplifyContracts(object contracts)
        {
            if (contracts is Dictionary<string, object> contractDict)
            {
                return new Dictionary<string, object>
                {
                    ["interface"] = ExtractInterfaceNames(contractDict),
                    ["dependencies"] = ExtractContractDependencies(contractDict)
                };
            }
            return new Dictionary<string, object>();
        }

        private List<string> ExtractInterfaceNames(Dictionary<string, object> contracts)
        {
            var interfaces = new List<string>();
            foreach (var contract in contracts.Values)
            {
                if (contract is Dictionary<string, object> contractDict &&
                    contractDict.ContainsKey("interface"))
                {
                    interfaces.Add(contractDict["interface"].ToString());
                }
            }
            return interfaces;
        }

        private List<string> ExtractContractDependencies(Dictionary<string, object> contracts)
        {
            var dependencies = new List<string>();
            foreach (var contract in contracts.Values)
            {
                if (contract is Dictionary<string, object> contractDict &&
                    contractDict.ContainsKey("dependencies"))
                {
                    dependencies.AddRange(((List<object>)contractDict["dependencies"])
                        .Select(d => d.ToString()));
                }
            }
            return dependencies.Distinct().ToList();
        }

        private object SimplifyDecisions(object decisions)
        {
            if (decisions is List<object> decisionList)
            {
                return decisionList.Select(d => new
                {
                    date = ((Dictionary<string, object>)d)["date"],
                    rationale = ((Dictionary<string, object>)d)["rationale"],
                    alternatives_considered = ((Dictionary<string, object>)d)["alternatives"]
                });
            }
            return new List<object>();
        }

        private List<string> SimplifyDependencies(object dependencies)
        {
            if (dependencies is List<object> depList)
            {
                return depList.Select(d => d.ToString()).Distinct().ToList();
            }
            return new List<string>();
        }

        private object SimplifyPatterns(object patterns)
        {
            if (patterns is List<object> patternList)
            {
                return patternList.Select(p => new
                {
                    name = ((Dictionary<string, object>)p)["name"],
                    type = ((Dictionary<string, object>)p)["type"],
                    usage = ((Dictionary<string, object>)p)["usage"]
                });
            }
            return new List<object>();
        }

        private List<string> ExtractReferences(string sourceCode)
        {
            // Simple reference extraction - in practice you'd want to use Roslyn
            var references = new List<string>();
            var lines = sourceCode.Split('\n');

            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("using "))
                {
                    var reference = line.Trim().TrimStart("using ".ToCharArray()).TrimEnd(';');
                    references.Add(reference);
                }
            }

            return references.Distinct().ToList();
        }

        private List<string> ExtractResponsibilities(string sourceCode)
        {
            // Simple responsibility extraction - in practice you'd want to use Roslyn
            var responsibilities = new List<string>();
            var lines = sourceCode.Split('\n');

            foreach (var line in lines)
            {
                // Extract from comments
                if (line.Contains("///") && line.Contains("Responsible for"))
                {
                    var responsibility = line.Substring(line.IndexOf("Responsible for") + 15).Trim();
                    responsibilities.Add(responsibility);
                }

                // Extract from method summaries
                if (line.Contains("[Description(") || line.Contains("[DisplayName("))
                {
                    var start = line.IndexOf("(\"") + 2;
                    var end = line.IndexOf("\")", start);
                    if (start >= 0 && end >= 0)
                    {
                        responsibilities.Add(line.Substring(start, end - start));
                    }
                }
            }

            return responsibilities.Distinct().ToList();
        }
    }
}