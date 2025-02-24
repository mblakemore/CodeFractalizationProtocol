using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using FractalCode.Core;
using FractalCode.Patterns;

namespace FractalCode.Monitoring
{
    public class HealthMetrics
    {
        public Dictionary<string, double> ComponentMetrics { get; set; } = new();
        public Dictionary<string, double> ResourceMetrics { get; set; } = new();
        public Dictionary<string, double> ContractMetrics { get; set; } = new();
        public List<HealthIssue> ActiveIssues { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class HealthIssue
    {
        public string ComponentId { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
    }

    public class MonitoringConfiguration
    {
        public Dictionary<string, double> Thresholds { get; set; } = new();
        public int MonitoringIntervalSeconds { get; set; } = 300;
        public int DataRetentionDays { get; set; } = 30;
        public List<string> EnabledChecks { get; set; } = new();
    }

    public interface IHealthMonitor
    {
        Task<HealthMetrics> GetHealthMetricsAsync();
        Task<List<HealthIssue>> GetActiveIssuesAsync();
        Task<bool> ValidateComponentHealthAsync(string componentId);
        Task UpdateMonitoringConfigAsync(MonitoringConfiguration config);
        Task<bool> CheckSystemHealthAsync();
    }

    public class HealthMonitor : IHealthMonitor
    {
        private readonly ILogger<HealthMonitor> _logger;
        private readonly IPatternRegistry _patternRegistry;
        private readonly IYamlProcessor _yamlProcessor;
        private readonly string _monitoringPath;
        private MonitoringConfiguration _config;

        private const double CRITICAL_THRESHOLD = 0.6;
        private const double WARNING_THRESHOLD = 0.8;
        private const string METRICS_FILE = "metrics.yaml";

        public HealthMonitor(
            ILogger<HealthMonitor> logger,
            IPatternRegistry patternRegistry,
            IYamlProcessor yamlProcessor,
            string monitoringPath = "monitoring")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _patternRegistry = patternRegistry ?? throw new ArgumentNullException(nameof(patternRegistry));
            _yamlProcessor = yamlProcessor ?? throw new ArgumentNullException(nameof(yamlProcessor));
            _monitoringPath = monitoringPath;
            _config = new MonitoringConfiguration();

            InitializeMonitoring().Wait();
        }

        public async Task<HealthMetrics> GetHealthMetricsAsync()
        {
            try
            {
                _logger.LogInformation("Collecting health metrics");

                var metrics = new HealthMetrics
                {
                    ComponentMetrics = await CollectComponentMetricsAsync(),
                    ResourceMetrics = await CollectResourceMetricsAsync(),
                    ContractMetrics = await CollectContractMetricsAsync(),
                    ActiveIssues = await GetActiveIssuesAsync(),
                    LastUpdated = DateTime.UtcNow
                };

                await SaveMetricsAsync(metrics);
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting health metrics");
                throw;
            }
        }

        public async Task<List<HealthIssue>> GetActiveIssuesAsync()
        {
            try
            {
                var issues = new List<HealthIssue>();

                // Check component health
                var componentIssues = await CheckComponentIssuesAsync();
                issues.AddRange(componentIssues);

                // Check resource health
                var resourceIssues = await CheckResourceIssuesAsync();
                issues.AddRange(resourceIssues);

                // Check contract health
                var contractIssues = await CheckContractIssuesAsync();
                issues.AddRange(contractIssues);

                // Add recommendations
                foreach (var issue in issues)
                {
                    issue.RecommendedActions = GenerateRecommendations(issue);
                }

                return issues;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active issues");
                throw;
            }
        }

        public async Task<bool> ValidateComponentHealthAsync(string componentId)
        {
            try
            {
                _logger.LogInformation($"Validating component health: {componentId}");

                // Get component metrics
                var metrics = await CollectComponentMetricsAsync(componentId);
                if (metrics == null)
                {
                    return false;
                }

                // Check against thresholds
                foreach (var (metric, value) in metrics)
                {
                    var threshold = _config.Thresholds.GetValueOrDefault($"{componentId}_{metric}", WARNING_THRESHOLD);
                    if (value < threshold)
                    {
                        _logger.LogWarning($"Component {componentId} failed health check: {metric} = {value:F2}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating component health: {componentId}");
                throw;
            }
        }

        public async Task UpdateMonitoringConfigAsync(MonitoringConfiguration config)
        {
            try
            {
                _config = config ?? throw new ArgumentNullException(nameof(config));
                await SaveConfigurationAsync();
                _logger.LogInformation("Monitoring configuration updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating monitoring configuration");
                throw;
            }
        }

        public async Task<bool> CheckSystemHealthAsync()
        {
            try
            {
                var metrics = await GetHealthMetricsAsync();

                // Check component health
                foreach (var (component, value) in metrics.ComponentMetrics)
                {
                    if (value < CRITICAL_THRESHOLD)
                    {
                        _logger.LogError($"Critical component health issue: {component}");
                        return false;
                    }
                }

                // Check resource health
                foreach (var (resource, value) in metrics.ResourceMetrics)
                {
                    if (value < CRITICAL_THRESHOLD)
                    {
                        _logger.LogError($"Critical resource health issue: {resource}");
                        return false;
                    }
                }

                // Check contract health
                foreach (var (contract, value) in metrics.ContractMetrics)
                {
                    if (value < CRITICAL_THRESHOLD)
                    {
                        _logger.LogError($"Critical contract health issue: {contract}");
                        return false;
                    }
                }

                return !metrics.ActiveIssues.Any(i => i.Severity == "Critical");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system health");
                throw;
            }
        }

        private async Task InitializeMonitoring()
        {
            try
            {
                // Create monitoring directory
                Directory.CreateDirectory(_monitoringPath);
                Directory.CreateDirectory(Path.Combine(_monitoringPath, "history"));

                // Load or create configuration
                var configPath = Path.Combine(_monitoringPath, "config.yaml");
                if (File.Exists(configPath))
                {
                    _config = await _yamlProcessor.DeserializeAsync<MonitoringConfiguration>(configPath);
                }
                else
                {
                    await SaveConfigurationAsync();
                }

                _logger.LogInformation("Monitoring initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing monitoring");
                throw;
            }
        }

        private async Task<Dictionary<string, double>> CollectComponentMetricsAsync()
        {
            var metrics = new Dictionary<string, double>();

            try
            {
                // Get all patterns
                var patterns = await _patternRegistry.GetAllPatternsAsync();

                foreach (var pattern in patterns)
                {
                    // Calculate pattern health
                    var health = CalculatePatternHealth(pattern);
                    metrics[pattern.Id] = health;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting component metrics");
            }

            return metrics;
        }

        private async Task<Dictionary<string, double>> CollectComponentMetricsAsync(string componentId)
        {
            try
            {
                var pattern = await _patternRegistry.GetPatternAsync(componentId);
                if (pattern == null)
                {
                    return null;
                }

                return new Dictionary<string, double>
                {
                    ["health"] = CalculatePatternHealth(pattern),
                    ["success_rate"] = pattern.SuccessRate,
                    ["usage_rate"] = CalculateUsageRate(pattern)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error collecting metrics for component: {componentId}");
                return null;
            }
        }

        private async Task<Dictionary<string, double>> CollectResourceMetricsAsync()
        {
            // Implement resource metric collection
            return new Dictionary<string, double>();
        }

        private async Task<Dictionary<string, double>> CollectContractMetricsAsync()
        {
            // Implement contract metric collection
            return new Dictionary<string, double>();
        }

        private double CalculatePatternHealth(Pattern pattern)
        {
            if (pattern.UsageCount == 0)
                return 0.5; // Default for unused patterns

            var health = pattern.SuccessRate;

            // Adjust for usage frequency
            var daysActive = (DateTime.UtcNow - pattern.DiscoveryDate).TotalDays;
            var usageRate = pattern.UsageCount / Math.Max(daysActive, 1);
            health *= Math.Min(usageRate * 10, 1.0); // Normalize usage rate

            return health;
        }

        private double CalculateUsageRate(Pattern pattern)
        {
            var daysActive = (DateTime.UtcNow - pattern.DiscoveryDate).TotalDays;
            return pattern.UsageCount / Math.Max(daysActive, 1);
        }

        private async Task<List<HealthIssue>> CheckComponentIssuesAsync()
        {
            var issues = new List<HealthIssue>();
            var patterns = await _patternRegistry.GetAllPatternsAsync();

            foreach (var pattern in patterns)
            {
                if (pattern.SuccessRate < CRITICAL_THRESHOLD)
                {
                    issues.Add(new HealthIssue
                    {
                        ComponentId = pattern.Id,
                        IssueType = "PatternReliability",
                        Description = $"Pattern success rate critically low: {pattern.SuccessRate:P}",
                        Severity = "Critical",
                        DetectedAt = DateTime.UtcNow
                    });
                }
                else if (pattern.SuccessRate < WARNING_THRESHOLD)
                {
                    issues.Add(new HealthIssue
                    {
                        ComponentId = pattern.Id,
                        IssueType = "PatternReliability",
                        Description = $"Pattern success rate low: {pattern.SuccessRate:P}",
                        Severity = "Warning",
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            return issues;
        }

        private async Task<List<HealthIssue>> CheckResourceIssuesAsync()
        {
            // Implement resource health checks
            return new List<HealthIssue>();
        }

        private async Task<List<HealthIssue>> CheckContractIssuesAsync()
        {
            // Implement contract health checks
            return new List<HealthIssue>();
        }

        private List<string> GenerateRecommendations(HealthIssue issue)
        {
            var recommendations = new List<string>();

            switch (issue.IssueType)
            {
                case "PatternReliability":
                    recommendations.Add("Review pattern implementation and validation criteria");
                    recommendations.Add("Analyze failure cases and update pattern constraints");
                    recommendations.Add("Consider adding additional validation steps");
                    break;

                case "ResourceUtilization":
                    recommendations.Add("Review resource allocation and scaling configuration");
                    recommendations.Add("Consider implementing load balancing");
                    recommendations.Add("Analyze usage patterns for optimization opportunities");
                    break;

                case "ContractCompliance":
                    recommendations.Add("Review and update contract implementation");
                    recommendations.Add("Verify contract compatibility with dependencies");
                    recommendations.Add("Consider updating contract validation rules");
                    break;

                default:
                    recommendations.Add("Review system logs for more information");
                    recommendations.Add("Monitor issue for pattern recognition");
                    break;
            }

            return recommendations;
        }

        private async Task SaveMetricsAsync(HealthMetrics metrics)
        {
            try
            {
                var metricsPath = Path.Combine(_monitoringPath, METRICS_FILE);
                await _yamlProcessor.SerializeAsync(metrics, metricsPath);

                // Save historical data
                var historyPath = Path.Combine(_monitoringPath, "history",
                    $"metrics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.yaml");
                await _yamlProcessor.SerializeAsync(metrics, historyPath);

                await CleanupHistoricalDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving metrics");
            }
        }

        private async Task SaveConfigurationAsync()
        {
            try
            {
                var configPath = Path.Combine(_monitoringPath, "config.yaml");
                await _yamlProcessor.SerializeAsync(_config, configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration");
            }
        }

        private async Task CleanupHistoricalDataAsync()
        {
            try
            {
                var retentionDate = DateTime.UtcNow.AddDays(-_config.DataRetentionDays);
                var historyDir = Path.Combine(_monitoringPath, "history");
                var oldFiles = Directory.GetFiles(historyDir, "metrics_*.yaml")
                    .Where(f => File.GetCreationTimeUtc(f) < retentionDate);

                foreach (var file in oldFiles)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up historical data");
            }
        }
    }
}