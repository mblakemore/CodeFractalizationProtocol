using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using FractalCode.Core;
using FractalCode.Impact;
using FractalCode.Analysis;

namespace FractalCode.Patterns
{
    public class PatternContext
    {
        public string OriginFractal { get; set; }
        public ValidationResult ValidationResults { get; set; }
        public Dictionary<string, double> PerformanceMetrics { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<string> ApplicabilityRules { get; set; } = new();
    }

    public class Pattern
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Implementation { get; set; }
        public List<string> ApplicabilityCriteria { get; set; }
        public Dictionary<string, object> ResourceRequirements { get; set; }
        public List<string> ValidationRules { get; set; }
        public Dictionary<string, double> PerformanceCharacteristics { get; set; }
        public DateTime DiscoveryDate { get; set; }
        public int UsageCount { get; set; }
        public double SuccessRate { get; set; }
    }

    public class SearchContext
    {
        public string TargetFractal { get; set; }
        public List<string> Requirements { get; set; }
        public Dictionary<string, object> Constraints { get; set; }
        public List<string> ExcludePatterns { get; set; }
    }

    public class UsageData
    {
        public string PatternId { get; set; }
        public string TargetFractal { get; set; }
        public DateTime ApplicationDate { get; set; }
        public bool Success { get; set; }
        public Dictionary<string, double> PerformanceImpact { get; set; }
        public List<string> Issues { get; set; }
    }

    public class UsageMetrics
    {
        public int TotalApplications { get; set; }
        public int SuccessfulApplications { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, double> AveragePerformanceImpact { get; set; }
        public List<string> CommonIssues { get; set; }
    }

    public interface IPatternRegistry
    {
        Task<string> RegisterPatternAsync(Pattern pattern, PatternContext context);
        Task<List<Pattern>> QueryPatternsAsync(SearchContext context);
        Task<UsageMetrics> TrackPatternUsageAsync(string patternId, UsageData usageData);
        Task<Pattern> GetPatternAsync(string patternId);
        Task<bool> UpdatePatternAsync(string patternId, Pattern pattern);
        Task<List<Pattern>> GetAllPatternsAsync();
    }

    public class PatternRegistry : IPatternRegistry
    {
        private readonly ILogger<PatternRegistry> _logger;
        private readonly IYamlProcessor _yamlProcessor;
        private readonly string _registryPath;
        private readonly object _lockObject = new object();

        private const double SUCCESS_RATE_THRESHOLD = 0.7;
        private const int MIN_USAGE_COUNT = 5;

        public PatternRegistry(
            ILogger<PatternRegistry> logger,
            IYamlProcessor yamlProcessor,
            string registryPath = "patterns")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _yamlProcessor = yamlProcessor ?? throw new ArgumentNullException(nameof(yamlProcessor));
            _registryPath = registryPath;

            // Ensure registry directory exists
            Directory.CreateDirectory(_registryPath);
        }

        public async Task<string> RegisterPatternAsync(Pattern pattern, PatternContext context)
        {
            try
            {
                _logger.LogInformation($"Registering new pattern: {pattern.Name}");

                // Validate pattern
                await ValidatePatternAsync(pattern, context);

                // Generate unique ID if not provided
                if (string.IsNullOrEmpty(pattern.Id))
                {
                    pattern.Id = GeneratePatternId(pattern);
                }

                // Set initial metrics
                pattern.DiscoveryDate = DateTime.UtcNow;
                pattern.UsageCount = 0;
                pattern.SuccessRate = 1.0;

                // Save pattern
                var patternPath = GetPatternPath(pattern.Id);
                await _yamlProcessor.SerializeAsync(pattern, patternPath);

                _logger.LogInformation($"Successfully registered pattern: {pattern.Id}");
                return pattern.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error registering pattern: {pattern.Name}");
                throw;
            }
        }

        public async Task<List<Pattern>> QueryPatternsAsync(SearchContext context)
        {
            try
            {
                _logger.LogInformation($"Querying patterns for fractal: {context.TargetFractal}");

                // Load all patterns
                var patterns = await GetAllPatternsAsync();

                // Filter by requirements
                var filteredPatterns = patterns.Where(p =>
                    MatchesRequirements(p, context.Requirements) &&
                    MeetsConstraints(p, context.Constraints) &&
                    !IsExcluded(p, context.ExcludePatterns) &&
                    IsReliable(p)
                ).ToList();

                // Sort by relevance
                filteredPatterns.Sort((a, b) => CalculateRelevance(b, context)
                    .CompareTo(CalculateRelevance(a, context)));

                return filteredPatterns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying patterns");
                throw;
            }
        }

        public async Task<UsageMetrics> TrackPatternUsageAsync(string patternId, UsageData usageData)
        {
            try
            {
                _logger.LogInformation($"Tracking usage for pattern: {patternId}");

                var pattern = await GetPatternAsync(patternId);
                if (pattern == null)
                {
                    throw new KeyNotFoundException($"Pattern not found: {patternId}");
                }

                // Update pattern metrics
                pattern.UsageCount++;
                pattern.SuccessRate = ((pattern.SuccessRate * (pattern.UsageCount - 1)) +
                    (usageData.Success ? 1 : 0)) / pattern.UsageCount;

                // Update performance characteristics
                UpdatePerformanceCharacteristics(pattern, usageData.PerformanceImpact);

                // Save updated pattern
                await UpdatePatternAsync(patternId, pattern);

                // Calculate and return usage metrics
                return new UsageMetrics
                {
                    TotalApplications = pattern.UsageCount,
                    SuccessfulApplications = (int)(pattern.UsageCount * pattern.SuccessRate),
                    SuccessRate = pattern.SuccessRate,
                    AveragePerformanceImpact = pattern.PerformanceCharacteristics,
                    CommonIssues = await GetCommonIssuesAsync(patternId)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error tracking pattern usage: {patternId}");
                throw;
            }
        }

        public async Task<Pattern> GetPatternAsync(string patternId)
        {
            try
            {
                var patternPath = GetPatternPath(patternId);
                if (!File.Exists(patternPath))
                {
                    return null;
                }

                return await _yamlProcessor.DeserializeAsync<Pattern>(patternPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving pattern: {patternId}");
                throw;
            }
        }

        public async Task<bool> UpdatePatternAsync(string patternId, Pattern pattern)
        {
            try
            {
                _logger.LogInformation($"Updating pattern: {patternId}");

                var patternPath = GetPatternPath(patternId);
                if (!File.Exists(patternPath))
                {
                    return false;
                }

                // Preserve ID
                pattern.Id = patternId;

                // Save updated pattern
                await _yamlProcessor.SerializeAsync(pattern, patternPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating pattern: {patternId}");
                throw;
            }
        }

        public async Task<List<Pattern>> GetAllPatternsAsync()
        {
            try
            {
                var patterns = new List<Pattern>();
                var patternFiles = Directory.GetFiles(_registryPath, "*.yaml");

                foreach (var file in patternFiles)
                {
                    var pattern = await _yamlProcessor.DeserializeAsync<Pattern>(file);
                    patterns.Add(pattern);
                }

                return patterns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all patterns");
                throw;
            }
        }

        private async Task ValidatePatternAsync(Pattern pattern, PatternContext context)
        {
            // Validate required fields
            if (string.IsNullOrEmpty(pattern.Name))
                throw new ArgumentException("Pattern name is required");

            if (string.IsNullOrEmpty(pattern.Type))
                throw new ArgumentException("Pattern type is required");

            if (pattern.Implementation == null || !pattern.Implementation.Any())
                throw new ArgumentException("Pattern implementation is required");

            // Validate context
            if (string.IsNullOrEmpty(context.OriginFractal))
                throw new ArgumentException("Origin fractal is required");

            if (context.ValidationResults == null)
                throw new ArgumentException("Validation results are required");

            // Check for duplicate patterns
            var existingPatterns = await GetAllPatternsAsync();
            if (existingPatterns.Any(p => p.Name == pattern.Name && p.Type == pattern.Type))
                throw new InvalidOperationException($"Pattern already exists: {pattern.Name}");
        }

        private string GeneratePatternId(Pattern pattern)
        {
            return $"{pattern.Type.ToLower()}-{pattern.Name.ToLower().Replace(" ", "-")}-{DateTime.UtcNow:yyyyMMdd}";
        }

        private string GetPatternPath(string patternId)
        {
            return Path.Combine(_registryPath, $"{patternId}.yaml");
        }

        private bool MatchesRequirements(Pattern pattern, List<string> requirements)
        {
            if (requirements == null || !requirements.Any())
                return true;

            return requirements.All(req =>
                pattern.ApplicabilityCriteria.Any(c =>
                    c.Contains(req, StringComparison.OrdinalIgnoreCase)));
        }

        private bool MeetsConstraints(Pattern pattern, Dictionary<string, object> constraints)
        {
            if (constraints == null || !constraints.Any())
                return true;

            foreach (var constraint in constraints)
            {
                if (pattern.ResourceRequirements.TryGetValue(constraint.Key, out var requirement))
                {
                    // Compare requirement with constraint
                    if (!MeetsConstraint(requirement, constraint.Value))
                        return false;
                }
            }

            return true;
        }

        private bool MeetsConstraint(object requirement, object constraint)
        {
            // Handle numeric comparisons
            if (requirement is IConvertible req && constraint is IConvertible con)
            {
                var reqValue = Convert.ToDouble(req);
                var conValue = Convert.ToDouble(con);
                return reqValue <= conValue;
            }

            // Handle string comparisons
            return requirement.ToString().Equals(
                constraint.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsExcluded(Pattern pattern, List<string> excludePatterns)
        {
            if (excludePatterns == null || !excludePatterns.Any())
                return false;

            return excludePatterns.Contains(pattern.Id);
        }

        private bool IsReliable(Pattern pattern)
        {
            return pattern.UsageCount >= MIN_USAGE_COUNT &&
                   pattern.SuccessRate >= SUCCESS_RATE_THRESHOLD;
        }

        private double CalculateRelevance(Pattern pattern, SearchContext context)
        {
            double relevance = 0;

            // Base relevance on success rate and usage count
            relevance += pattern.SuccessRate * 0.4;
            relevance += Math.Min(pattern.UsageCount / 20.0, 1.0) * 0.3;

            // Add points for matching requirements
            if (context.Requirements != null)
            {
                var matchingCriteria = context.Requirements.Count(req =>
                    pattern.ApplicabilityCriteria.Any(c =>
                        c.Contains(req, StringComparison.OrdinalIgnoreCase)));
                relevance += (matchingCriteria / (double)context.Requirements.Count) * 0.3;
            }

            return relevance;
        }

        private void UpdatePerformanceCharacteristics(Pattern pattern, Dictionary<string, double> newMetrics)
        {
            if (pattern.PerformanceCharacteristics == null)
            {
                pattern.PerformanceCharacteristics = new Dictionary<string, double>();
            }

            foreach (var metric in newMetrics)
            {
                if (pattern.PerformanceCharacteristics.ContainsKey(metric.Key))
                {
                    // Running average
                    pattern.PerformanceCharacteristics[metric.Key] =
                        ((pattern.PerformanceCharacteristics[metric.Key] * (pattern.UsageCount - 1)) +
                        metric.Value) / pattern.UsageCount;
                }
                else
                {
                    pattern.PerformanceCharacteristics[metric.Key] = metric.Value;
                }
            }
        }

        private async Task<List<string>> GetCommonIssuesAsync(string patternId)
        {
            try
            {
                var issuesPath = Path.Combine(_registryPath, $"{patternId}-issues.yaml");
                if (!File.Exists(issuesPath))
                {
                    return new List<string>();
                }

                var issues = await _yamlProcessor.DeserializeAsync<List<string>>(issuesPath);
                return issues.GroupBy(i => i)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving common issues for pattern: {patternId}");
                return new List<string>();
            }
        }
    }
}