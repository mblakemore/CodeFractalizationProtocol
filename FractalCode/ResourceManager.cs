using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using FractalCode.Core;
using FractalCode.Contracts;
using FractalCode.Core.Models;

namespace FractalCode.Core
{
    public class ResourceTracker
    {
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public Dictionary<string, double> Metrics { get; set; }
        public List<string> Dependencies { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class OptimizationConstraints
    {
        public Dictionary<string, double> ResourceLimits { get; set; }
        public Dictionary<string, double> PerformanceTargets { get; set; }
        public List<string> OptimizationRules { get; set; }
        public Dictionary<string, object> Context { get; set; }
    }

    public class OptimizationPlan
    {
        public string ResourceId { get; set; }
        public List<OptimizationStep> Steps { get; set; }
        public Dictionary<string, double> ExpectedImprovements { get; set; }
        public Dictionary<string, object> ValidationCriteria { get; set; }
        public string RollbackProcedure { get; set; }
    }

    public class OptimizationStep
    {
        public string Action { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, double> ExpectedImpact { get; set; }
        public List<string> ValidationChecks { get; set; }
        public string RollbackAction { get; set; }
    }

    public class FractalContext
    {
        public string FractalId { get; set; }
        public Dictionary<string, object> State { get; set; }
        public List<string> ActiveContracts { get; set; }
        public Dictionary<string, object> ResourceConstraints { get; set; }
    }

    public interface IResourceManager
    {
        Task<ResourceTracker> TrackResourceAsync(Resource resource, FractalContext context);
        Task<OptimizationPlan> OptimizeResourceAsync(string resourceId, OptimizationConstraints constraints);
        Task<bool> ValidateResourceAsync(string resourceId, Dictionary<string, object> criteria);
        Task<ResourceTracker> GetResourceStatusAsync(string resourceId);
        Task<bool> UpdateResourceAsync(string resourceId, Dictionary<string, object> updates);
    }

    public class ResourceManager : IResourceManager
    {
        private readonly ILogger<ResourceManager> _logger;
        private readonly IYamlProcessor _yamlProcessor;
        private readonly IContractValidator _contractValidator;
        private readonly string _resourcePath;
        private readonly Dictionary<string, ResourceTracker> _activeResources;
        private readonly object _lockObject = new object();

        private const double OPTIMIZATION_THRESHOLD = 0.2;
        private const int METRIC_HISTORY_LENGTH = 100;

        public ResourceManager(
            ILogger<ResourceManager> logger,
            IYamlProcessor yamlProcessor,
            IContractValidator contractValidator,
            string resourcePath = "resources")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _yamlProcessor = yamlProcessor ?? throw new ArgumentNullException(nameof(yamlProcessor));
            _contractValidator = contractValidator ?? throw new ArgumentNullException(nameof(contractValidator));
            _resourcePath = resourcePath;
            _activeResources = new Dictionary<string, ResourceTracker>();

            // Ensure resource directory exists
            Directory.CreateDirectory(_resourcePath);
        }

        public async Task<ResourceTracker> TrackResourceAsync(Resource resource, FractalContext context)
        {
            try
            {
                _logger.LogInformation($"Starting resource tracking for: {resource.Id}");

                // Validate resource and context
                await ValidateResourceAndContextAsync(resource, context);

                // Create resource tracker
                var tracker = new ResourceTracker
                {
                    ResourceId = resource.Id,
                    ResourceType = resource.Type,
                    Metadata = resource.Metadata ?? new Dictionary<string, object>(),
                    Metrics = InitializeResourceMetrics(resource),
                    Dependencies = await GetResourceDependenciesAsync(resource, context),
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                // Save tracker state
                await SaveResourceStateAsync(tracker);

                // Add to active resources
                lock (_lockObject)
                {
                    _activeResources[resource.Id] = tracker;
                }

                // Setup monitoring
                await SetupResourceMonitoringAsync(tracker, context);

                return tracker;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error tracking resource: {resource.Id}");
                throw;
            }
        }

        public async Task<OptimizationPlan> OptimizeResourceAsync(string resourceId, OptimizationConstraints constraints)
        {
            try
            {
                _logger.LogInformation($"Optimizing resource: {resourceId}");

                // Get current resource status
                var tracker = await GetResourceStatusAsync(resourceId);
                if (tracker == null)
                {
                    throw new KeyNotFoundException($"Resource not found: {resourceId}");
                }

                // Analyze current performance
                var analysis = await AnalyzeResourcePerformanceAsync(tracker);

                // Generate optimization steps
                var steps = await GenerateOptimizationStepsAsync(tracker, analysis, constraints);

                // Create optimization plan
                var plan = new OptimizationPlan
                {
                    ResourceId = resourceId,
                    Steps = steps,
                    ExpectedImprovements = CalculateExpectedImprovements(steps, constraints),
                    ValidationCriteria = GenerateValidationCriteria(steps, constraints),
                    RollbackProcedure = GenerateRollbackProcedure(steps)
                };

                // Validate plan
                await ValidateOptimizationPlanAsync(plan, constraints);

                return plan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error optimizing resource: {resourceId}");
                throw;
            }
        }

        public async Task<bool> ValidateResourceAsync(string resourceId, Dictionary<string, object> criteria)
        {
            try
            {
                _logger.LogInformation($"Validating resource: {resourceId}");

                var tracker = await GetResourceStatusAsync(resourceId);
                if (tracker == null)
                {
                    return false;
                }

                // Validate current state
                foreach (var criterion in criteria)
                {
                    if (!await ValidateCriterionAsync(tracker, criterion.Key, criterion.Value))
                    {
                        _logger.LogWarning($"Resource {resourceId} failed validation: {criterion.Key}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating resource: {resourceId}");
                throw;
            }
        }

        public async Task<ResourceTracker> GetResourceStatusAsync(string resourceId)
        {
            try
            {
                // Check active resources first
                lock (_lockObject)
                {
                    if (_activeResources.TryGetValue(resourceId, out var tracker))
                    {
                        return tracker;
                    }
                }

                // Load from storage
                var resourcePath = GetResourcePath(resourceId);
                if (!File.Exists(resourcePath))
                {
                    return null;
                }

                var loadedTracker = await _yamlProcessor.DeserializeAsync<ResourceTracker>(resourcePath);

                // Add to active resources
                lock (_lockObject)
                {
                    _activeResources[resourceId] = loadedTracker;
                }

                return loadedTracker;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting resource status: {resourceId}");
                throw;
            }
        }

        public async Task<bool> UpdateResourceAsync(string resourceId, Dictionary<string, object> updates)
        {
            try
            {
                _logger.LogInformation($"Updating resource: {resourceId}");

                var tracker = await GetResourceStatusAsync(resourceId);
                if (tracker == null)
                {
                    return false;
                }

                // Apply updates
                foreach (var update in updates)
                {
                    if (tracker.Metadata.ContainsKey(update.Key))
                    {
                        tracker.Metadata[update.Key] = update.Value;
                    }
                    else
                    {
                        tracker.Metadata.Add(update.Key, update.Value);
                    }
                }

                tracker.LastUpdated = DateTime.UtcNow;

                // Save updated state
                await SaveResourceStateAsync(tracker);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating resource: {resourceId}");
                throw;
            }
        }

        private async Task ValidateResourceAndContextAsync(Resource resource, FractalContext context)
        {
            // Validate resource
            if (string.IsNullOrEmpty(resource.Id))
                throw new ArgumentException("Resource ID is required");

            if (string.IsNullOrEmpty(resource.Type))
                throw new ArgumentException("Resource type is required");

            // Validate context
            if (string.IsNullOrEmpty(context.FractalId))
                throw new ArgumentException("Fractal ID is required");

            // Validate against active contracts
            if (context.ActiveContracts?.Any() == true)
            {
                foreach (var contract in context.ActiveContracts)
                {
                    var validation = await _contractValidator.ValidateContractAsync(
                        contract,
                        "resource"
                    );

                    if (!validation.IsValid)
                    {
                        throw new InvalidOperationException(
                            $"Resource contract validation failed: {string.Join(", ", validation.Errors)}"
                        );
                    }
                }
            }
        }

        private Dictionary<string, double> InitializeResourceMetrics(Resource resource)
        {
            return new Dictionary<string, double>
            {
                ["utilization"] = 0.0,
                ["availability"] = 100.0,
                ["performance"] = 100.0,
                ["error_rate"] = 0.0
            };
        }

        private async Task<List<string>> GetResourceDependenciesAsync(Resource resource, FractalContext context)
        {
            var dependencies = new List<string>();

            // Extract from resource definition
            if (resource.Dependencies != null)
            {
                dependencies.AddRange(resource.Dependencies);
            }

            // Extract from contracts
            if (context.ActiveContracts?.Any() == true)
            {
                foreach (var contract in context.ActiveContracts)
                {
                    var contractContent = await File.ReadAllTextAsync(contract);
                    var contractData = _yamlProcessor.DeserializeAsync<Dictionary<string, object>>(contract);
                    if (contractData.Result.ContainsKey("dependencies"))
                    {
                        dependencies.AddRange(((List<object>)contractData.Result["dependencies"])
                            .Select(d => d.ToString()));
                    }
                }
            }

            return dependencies.Distinct().ToList();
        }

        private string GetResourcePath(string resourceId)
        {
            return Path.Combine(_resourcePath, $"{resourceId}.yaml");
        }

        private async Task SaveResourceStateAsync(ResourceTracker tracker)
        {
            var resourcePath = GetResourcePath(tracker.ResourceId);
            await _yamlProcessor.SerializeAsync(tracker, resourcePath);
        }

        private async Task SetupResourceMonitoringAsync(ResourceTracker tracker, FractalContext context)
        {
            // Setup basic monitoring
            await SetupMetricMonitoringAsync(tracker);

            // Setup contract-specific monitoring
            if (context.ActiveContracts?.Any() == true)
            {
                await SetupContractMonitoringAsync(tracker, context.ActiveContracts);
            }

            // Setup dependency monitoring
            if (tracker.Dependencies.Any())
            {
                await SetupDependencyMonitoringAsync(tracker);
            }
        }

        private async Task SetupMetricMonitoringAsync(ResourceTracker tracker)
        {
            // Implementation would set up actual metric collection
            _logger.LogInformation($"Setting up metric monitoring for: {tracker.ResourceId}");
        }

        private async Task SetupContractMonitoringAsync(ResourceTracker tracker, List<string> contracts)
        {
            // Implementation would set up contract compliance monitoring
            _logger.LogInformation($"Setting up contract monitoring for: {tracker.ResourceId}");
        }

        private async Task SetupDependencyMonitoringAsync(ResourceTracker tracker)
        {
            // Implementation would set up dependency monitoring
            _logger.LogInformation($"Setting up dependency monitoring for: {tracker.ResourceId}");
        }

        private async Task<Dictionary<string, double>> AnalyzeResourcePerformanceAsync(ResourceTracker tracker)
        {
            // Implementation would analyze actual performance metrics
            return new Dictionary<string, double>
            {
                ["utilization_efficiency"] = CalculateUtilizationEfficiency(tracker),
                ["performance_score"] = CalculatePerformanceScore(tracker),
                ["resource_efficiency"] = CalculateResourceEfficiency(tracker)
            };
        }

        private double CalculateUtilizationEfficiency(ResourceTracker tracker)
        {
            return tracker.Metrics["utilization"] / 100.0;
        }

        private double CalculatePerformanceScore(ResourceTracker tracker)
        {
            return (tracker.Metrics["performance"] * (100 - tracker.Metrics["error_rate"])) / 10000.0;
        }

        private double CalculateResourceEfficiency(ResourceTracker tracker)
        {
            return (tracker.Metrics["availability"] * tracker.Metrics["performance"]) / 10000.0;
        }

        private async Task<List<OptimizationStep>> GenerateOptimizationStepsAsync(
            ResourceTracker tracker,
            Dictionary<string, double> analysis,
            OptimizationConstraints constraints)
        {
            var steps = new List<OptimizationStep>();

            // Check utilization optimization
            if (analysis["utilization_efficiency"] < OPTIMIZATION_THRESHOLD)
            {
                steps.Add(new OptimizationStep
                {
                    Action = "optimize_utilization",
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_utilization"] = Math.Min(
                            tracker.Metrics["utilization"] * 1.2,
                            constraints.ResourceLimits["max_utilization"]
                        )
                    },
                    ExpectedImpact = new Dictionary<string, double>
                    {
                        ["utilization"] = 20.0,
                        ["performance"] = -5.0
                    },
                    ValidationChecks = new List<string>
            {
                "check_performance_impact",
                "verify_stability"
            },
                    RollbackAction = "restore_utilization"
                });

                // Check performance optimization
                if (analysis["performance_score"] < OPTIMIZATION_THRESHOLD)
                {
                    steps.Add(new OptimizationStep
                    {
                        Action = "optimize_performance",
                        Parameters = new Dictionary<string, object>
                        {
                            ["target_performance"] = Math.Min(
                                tracker.Metrics["performance"] * 1.2,
                                constraints.PerformanceTargets["min_performance"]
                            )
                        },
                        ExpectedImpact = new Dictionary<string, double>
                        {
                            ["performance"] = 20.0,
                            ["utilization"] = 10.0
                        },
                        ValidationChecks = new List<string>
                {
                    "verify_performance_improvement",
                    "check_resource_impact"
                },
                        RollbackAction = "restore_performance"
                    });
                }

                // Check resource efficiency optimization
                if (analysis["resource_efficiency"] < OPTIMIZATION_THRESHOLD)
                {
                    steps.Add(new OptimizationStep
                    {
                        Action = "optimize_resource_usage",
                        Parameters = new Dictionary<string, object>
                        {
                            ["target_efficiency"] = Math.Min(
                                analysis["resource_efficiency"] * 1.3,
                                constraints.ResourceLimits["max_efficiency"]
                            )
                        },
                        ExpectedImpact = new Dictionary<string, double>
                        {
                            ["efficiency"] = 30.0,
                            ["utilization"] = -10.0
                        },
                        ValidationChecks = new List<string>
                {
                    "verify_efficiency_improvement",
                    "check_system_stability"
                },
                        RollbackAction = "restore_resource_config"
                    });
                }

                return steps;
            }
            // Handle case where initial condition is not met
            return steps;
            Dictionary<string, double> CalculateExpectedImprovements(List<OptimizationStep> steps)
            {
                var improvements = new Dictionary<string, double>();

                foreach (var step in steps)
                {
                    foreach (var impact in step.ExpectedImpact)
                    {
                        if (improvements.ContainsKey(impact.Key))
                        {
                            improvements[impact.Key] += impact.Value;
                        }
                        else
                        {
                            improvements[impact.Key] = impact.Value;
                        }
                    }
                }

                return improvements;
            }

            Dictionary<string, object> GenerateValidationCriteria(
                List<OptimizationStep> steps,
                OptimizationConstraints constraints)
            {
                var criteria = new Dictionary<string, object>();

                // Combine all validation checks
                var allChecks = steps.SelectMany(s => s.ValidationChecks).Distinct();

                foreach (var check in allChecks)
                {
                    switch (check)
                    {
                        case "check_performance_impact":
                            criteria["min_performance"] = constraints.PerformanceTargets["min_performance"];
                            break;

                        case "verify_stability":
                            criteria["max_error_rate"] = constraints.ResourceLimits["max_error_rate"];
                            break;

                        case "verify_performance_improvement":
                            criteria["performance_improvement"] = 15.0; // Minimum 15% improvement
                            break;

                        case "check_resource_impact":
                            criteria["max_resource_usage"] = constraints.ResourceLimits["max_resource_usage"];
                            break;

                        case "verify_efficiency_improvement":
                            criteria["efficiency_improvement"] = 20.0; // Minimum 20% improvement
                            break;

                        case "check_system_stability":
                            criteria["stability_threshold"] = 0.95; // 95% stability required
                            break;
                    }
                }

                return criteria;
            }

            string GenerateRollbackProcedure(List<OptimizationStep> steps)
            {
                // Create rollback sequence in reverse order
                var rollbackSteps = steps
                    .Select(s => s.RollbackAction)
                    .Reverse()
                    .ToList();

                return string.Join("\n", rollbackSteps);
            }

            async Task ValidateOptimizationPlanAsync(
                OptimizationPlan plan,
                OptimizationConstraints constraints)
            {
                // Validate each step
                foreach (var step in plan.Steps)
                {
                    // Validate expected impact against constraints
                    foreach (var impact in step.ExpectedImpact)
                    {
                        if (constraints.ResourceLimits.ContainsKey($"max_{impact.Key}") &&
                            impact.Value > constraints.ResourceLimits[$"max_{impact.Key}"])
                        {
                            throw new InvalidOperationException(
                                $"Step {step.Action} exceeds resource limit for {impact.Key}"
                            );
                        }

                        if (constraints.PerformanceTargets.ContainsKey($"min_{impact.Key}") &&
                            impact.Value < constraints.PerformanceTargets[$"min_{impact.Key}"])
                        {
                            throw new InvalidOperationException(
                                $"Step {step.Action} fails to meet performance target for {impact.Key}"
                            );
                        }
                    }

                    // Validate each check has corresponding validation criteria
                    foreach (var check in step.ValidationChecks)
                    {
                        if (!plan.ValidationCriteria.ContainsKey(GetValidationCriterionKey(check)))
                        {
                            throw new InvalidOperationException(
                                $"Missing validation criteria for check: {check}"
                            );
                        }
                    }

                    // Validate rollback action exists
                    if (string.IsNullOrEmpty(step.RollbackAction))
                    {
                        throw new InvalidOperationException(
                            $"Missing rollback action for step: {step.Action}"
                        );
                    }
                }
            }

            async Task<bool> ValidateCriterionAsync(
        ResourceTracker resourceTracker, // Renamed parameter
        string criterion,
        object expectedValue)
            {
                // Get current value
                var currentValue = await GetResourceMetricAsync(resourceTracker, criterion);

                // Compare based on type
                if (currentValue is double numericValue && expectedValue is double expectedNumeric)
                {
                    return numericValue >= expectedNumeric;
                }

                if (currentValue is bool boolValue && expectedValue is bool expectedBool)
                {
                    return boolValue == expectedBool;
                }

                // String comparison
                return currentValue.ToString().Equals(
                    expectedValue.ToString(),
                    StringComparison.OrdinalIgnoreCase
                );
            }

            async Task<object> GetResourceMetricAsync(ResourceTracker tracker, string metric)
            {
                // Check metrics dictionary first
                if (tracker.Metrics.ContainsKey(metric))
                {
                    return tracker.Metrics[metric];
                }

                // Check metadata
                if (tracker.Metadata.ContainsKey(metric))
                {
                    return tracker.Metadata[metric];
                }

                // Return null if metric not found
                return null;
            }

            string GetValidationCriterionKey(string check)
            {
                switch (check)
                {
                    case "check_performance_impact":
                        return "min_performance";
                    case "verify_stability":
                        return "max_error_rate";
                    case "verify_performance_improvement":
                        return "performance_improvement";
                    case "check_resource_impact":
                        return "max_resource_usage";
                    case "verify_efficiency_improvement":
                        return "efficiency_improvement";
                    case "check_system_stability":
                        return "stability_threshold";
                    default:
                        return check.ToLower().Replace("check_", "").Replace("verify_", "");
                }
            }
        }
        /// <summary>
        /// Calculates expected improvements from optimization steps
        /// </summary>
        private Dictionary<string, double> CalculateExpectedImprovements(
            List<OptimizationStep> steps,
            OptimizationConstraints constraints,
            Dictionary<string, double> baselineMetrics = null)
        {
            try
            {
                baselineMetrics ??= new Dictionary<string, double>();
                var improvements = new Dictionary<string, double>();

                // Constants for improvement calculations
                const double MAX_UTILIZATION_IMPROVEMENT = 0.30; // 30% max improvement
                const double MAX_PERFORMANCE_IMPROVEMENT = 0.25; // 25% max improvement
                const double MAX_EFFICIENCY_IMPROVEMENT = 0.35; // 35% max improvement
                const double DIMINISHING_RETURNS_THRESHOLD = 0.80; // 80% threshold

                foreach (var step in steps)
                {
                    // Calculate step-specific improvements
                    var stepImprovements = CalculateStepImprovements(step, baselineMetrics);

                    // Merge improvements with consideration for diminishing returns
                    foreach (var (metric, value) in stepImprovements)
                    {
                        if (improvements.ContainsKey(metric))
                        {
                            improvements[metric] = CalculateCombinedImprovement(
                                improvements[metric],
                                value,
                                baselineMetrics.GetValueOrDefault(metric, 0),
                                DIMINISHING_RETURNS_THRESHOLD
                            );
                        }
                        else
                        {
                            improvements[metric] = value;
                        }
                    }
                }

                // Apply constraint limitations
                ApplyConstraintLimits(improvements, constraints);

                // Validate improvements
                ValidateImprovements(improvements, constraints);

                return improvements;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating expected improvements");
                throw;
            }
        }

        /// <summary>
        /// Calculates improvements for a specific optimization step
        /// </summary>
        private Dictionary<string, double> CalculateStepImprovements(
            OptimizationStep step,
            Dictionary<string, double> baselineMetrics)
        {
            var improvements = new Dictionary<string, double>();

            switch (step.Action)
            {
                case "optimize_utilization":
                    improvements["utilization"] = CalculateUtilizationImprovement(step, baselineMetrics);
                    improvements["performance"] = -0.05; // Small performance trade-off
                    break;

                case "optimize_performance":
                    improvements["performance"] = CalculatePerformanceImprovement(step, baselineMetrics);
                    improvements["utilization"] = 0.10; // Moderate utilization improvement
                    break;

                case "optimize_resource_usage":
                    improvements["efficiency"] = CalculateEfficiencyImprovement(step, baselineMetrics);
                    improvements["utilization"] = -0.10; // Resource usage trade-off
                    break;
            }

            return improvements;
        }

        /// <summary>
        /// Calculates combined improvement with diminishing returns
        /// </summary>
        private double CalculateCombinedImprovement(
            double existing,
            double additional,
            double baseline,
            double diminishingReturnsThreshold)
        {
            // Apply diminishing returns if above threshold
            if (baseline + existing > diminishingReturnsThreshold)
            {
                additional *= (1 - ((baseline + existing - diminishingReturnsThreshold) /
                                  (1 - diminishingReturnsThreshold)));
            }

            return Math.Min(existing + additional, GetMaxImprovement(baseline));
        }

        private double CalculateUtilizationImprovement(
            OptimizationStep step,
            Dictionary<string, double> baselineMetrics)
        {
            const double MAX_UTILIZATION_IMPROVEMENT = 0.30;

            if (step.Parameters.TryGetValue("target_utilization", out var target))
            {
                double currentUtilization = baselineMetrics.GetValueOrDefault("utilization", 0);
                double targetUtilization = Convert.ToDouble(target);

                return Math.Min(
                    (targetUtilization - currentUtilization) / currentUtilization,
                    MAX_UTILIZATION_IMPROVEMENT
                );
            }
            return 0;
        }

        private double CalculatePerformanceImprovement(
            OptimizationStep step,
            Dictionary<string, double> baselineMetrics)
        {
            const double MAX_PERFORMANCE_IMPROVEMENT = 0.25;

            if (step.Parameters.TryGetValue("target_performance", out var target))
            {
                double currentPerformance = baselineMetrics.GetValueOrDefault("performance", 0);
                double targetPerformance = Convert.ToDouble(target);

                return Math.Min(
                    (targetPerformance - currentPerformance) / currentPerformance,
                    MAX_PERFORMANCE_IMPROVEMENT
                );
            }
            return 0;
        }

        private double CalculateEfficiencyImprovement(
            OptimizationStep step,
            Dictionary<string, double> baselineMetrics)
        {
            const double MAX_EFFICIENCY_IMPROVEMENT = 0.35;

            if (step.Parameters.TryGetValue("target_efficiency", out var target))
            {
                double currentEfficiency = baselineMetrics.GetValueOrDefault("efficiency", 0);
                double targetEfficiency = Convert.ToDouble(target);

                return Math.Min(
                    (targetEfficiency - currentEfficiency) / currentEfficiency,
                    MAX_EFFICIENCY_IMPROVEMENT
                );
            }
            return 0;
        }

        private double GetMaxImprovement(double baseline)
        {
            // Higher baseline means lower potential improvement
            return Math.Max(0, 1 - baseline);
        }

        private void ApplyConstraintLimits(Dictionary<string, double> improvements, OptimizationConstraints constraints)
        {
            if (constraints?.ResourceLimits == null) return;

            foreach (var (metric, value) in improvements.ToList())
            {
                if (constraints.ResourceLimits.TryGetValue($"max_{metric}", out var limit))
                {
                    improvements[metric] = Math.Min(value, limit);
                }
            }
        }

        private void ValidateImprovements(Dictionary<string, double> improvements, OptimizationConstraints constraints)
        {
            if (constraints?.ResourceLimits == null) return;

            foreach (var (metric, value) in improvements)
            {
                if (constraints.ResourceLimits.TryGetValue($"max_{metric}", out var limit) && value > limit)
                {
                    _logger.LogWarning(
                        $"Improvement for {metric} ({value:P}) exceeds configured threshold ({limit:P})"
                    );
                }
            }
        }

        /// <summary>
        /// Generates validation criteria for optimization steps based on constraints
        /// </summary>
        private Dictionary<string, object> GenerateValidationCriteria(List<OptimizationStep> steps, OptimizationConstraints constraints)
        {
            try
            {
                var criteria = new Dictionary<string, object>();

                // Process each step's validation checks
                foreach (var step in steps)
                {
                    foreach (var check in step.ValidationChecks)
                    {
                        switch (check)
                        {
                            case "check_performance_impact":
                                criteria["min_performance"] = constraints.PerformanceTargets.GetValueOrDefault(
                                    "min_performance",
                                    0.8  // Default 80% minimum performance
                                );
                                break;

                            case "verify_stability":
                                criteria["max_error_rate"] = constraints.ResourceLimits.GetValueOrDefault(
                                    "max_error_rate",
                                    0.01  // Default 1% error rate
                                );
                                criteria["stability_threshold"] = 0.95;  // 95% stability required
                                break;

                            case "verify_performance_improvement":
                                criteria["performance_improvement"] = step.ExpectedImpact.GetValueOrDefault(
                                    "performance",
                                    0.15  // Default 15% improvement
                                );
                                break;

                            case "check_resource_impact":
                                criteria["max_resource_usage"] = constraints.ResourceLimits.GetValueOrDefault(
                                    "max_resource_usage",
                                    0.9  // Default 90% resource usage limit
                                );
                                break;

                            case "verify_efficiency_improvement":
                                criteria["efficiency_improvement"] = step.ExpectedImpact.GetValueOrDefault(
                                    "efficiency",
                                    0.2  // Default 20% improvement
                                );
                                break;

                            case "check_system_stability":
                                criteria["stability_duration"] = TimeSpan.FromMinutes(30);  // Monitor stability for 30 minutes
                                criteria["stability_threshold"] = 0.95;  // 95% stability required
                                break;

                            case "verify_output_quality":
                                criteria["quality_threshold"] = 0.95;  // 95% quality required
                                criteria["quality_metrics"] = new[]
                                {
                            "accuracy",
                            "consistency",
                            "reliability"
                        };
                                break;

                            case "check_concurrent_operations":
                                criteria["max_concurrent_ops"] = constraints.ResourceLimits.GetValueOrDefault(
                                    "max_concurrent_operations",
                                    100  // Default max concurrent operations
                                );
                                criteria["response_time_threshold"] = TimeSpan.FromMilliseconds(200);
                                break;
                        }
                    }

                    // Add step-specific criteria based on parameters
                    if (step.Parameters != null)
                    {
                        foreach (var (key, value) in step.Parameters)
                        {
                            var criteriaKey = $"param_{key}";
                            if (!criteria.ContainsKey(criteriaKey))
                            {
                                criteria[criteriaKey] = value;
                            }
                        }
                    }
                }

                // Add general validation criteria
                criteria["validation_timeout"] = TimeSpan.FromMinutes(5);  // Maximum validation time
                criteria["retry_count"] = 3;  // Number of validation attempts
                criteria["validation_interval"] = TimeSpan.FromSeconds(30);  // Time between validation checks

                // Add resource-specific criteria
                if (constraints.ResourceLimits != null)
                {
                    foreach (var (resource, limit) in constraints.ResourceLimits)
                    {
                        var criteriaKey = $"resource_limit_{resource}";
                        if (!criteria.ContainsKey(criteriaKey))
                        {
                            criteria[criteriaKey] = limit;
                        }
                    }
                }

                // Add performance-specific criteria
                if (constraints.PerformanceTargets != null)
                {
                    foreach (var (metric, target) in constraints.PerformanceTargets)
                    {
                        var criteriaKey = $"performance_target_{metric}";
                        if (!criteria.ContainsKey(criteriaKey))
                        {
                            criteria[criteriaKey] = target;
                        }
                    }
                }

                // Log validation criteria
                _logger.LogDebug("Generated validation criteria: {Criteria}",
                    string.Join(", ", criteria.Select(c => $"{c.Key}={c.Value}")));

                return criteria;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating validation criteria");
                throw;
            }
        }

        /// <summary>
        /// Generates rollback procedure for optimization steps
        /// </summary>
        private string GenerateRollbackProcedure(List<OptimizationStep> steps)
        {
            try
            {
                var rollbackSteps = new List<string>();
                var resourceStates = new Dictionary<string, object>();

                // Process steps in reverse order for rollback
                foreach (var step in steps.AsEnumerable().Reverse())
                {
                    // Record resource state before generating rollback
                    if (step.Parameters != null)
                    {
                        foreach (var (param, value) in step.Parameters)
                        {
                            var stateKey = $"{step.Action}_{param}";
                            if (!resourceStates.ContainsKey(stateKey))
                            {
                                resourceStates[stateKey] = value;
                            }
                        }
                    }

                    // Generate rollback step based on action type
                    switch (step.Action)
                    {
                        case "optimize_utilization":
                            rollbackSteps.Add(GenerateUtilizationRollback(step, resourceStates));
                            break;

                        case "optimize_performance":
                            rollbackSteps.Add(GeneratePerformanceRollback(step, resourceStates));
                            break;

                        case "optimize_resource_usage":
                            rollbackSteps.Add(GenerateResourceUsageRollback(step, resourceStates));
                            break;

                        default:
                            // Generate generic rollback if specific handler not available
                            rollbackSteps.Add(GenerateDefaultRollback(step));
                            break;
                    }

                    // Add verification step after each rollback
                    rollbackSteps.Add(GenerateVerificationStep(step));
                }

                // Add general cleanup and verification steps
                rollbackSteps.Add("verify_system_stability");
                rollbackSteps.Add("cleanup_temporary_resources");
                rollbackSteps.Add("restore_monitoring_configuration");

                // Log rollback procedure
                _logger.LogDebug("Generated rollback procedure with {Count} steps", rollbackSteps.Count);

                // Return formatted rollback procedure
                return FormatRollbackProcedure(rollbackSteps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating rollback procedure");
                throw;
            }
        }

        private string GenerateUtilizationRollback(OptimizationStep step, Dictionary<string, object> states)
        {
            var rollback = new List<string>();

            if (step.Parameters.TryGetValue("target_utilization", out var target))
            {
                rollback.Add($"restore_utilization_target:{target}");
                rollback.Add("adjust_resource_allocation");
                rollback.Add("revert_scaling_configuration");
            }

            if (step.Parameters.TryGetValue("resource_pool", out var pool))
            {
                rollback.Add($"restore_resource_pool:{pool}");
            }

            return string.Join(";", rollback);
        }

        private string GeneratePerformanceRollback(OptimizationStep step, Dictionary<string, object> states)
        {
            var rollback = new List<string>();

            if (step.Parameters.TryGetValue("target_performance", out var target))
            {
                rollback.Add($"restore_performance_target:{target}");
                rollback.Add("reset_performance_optimizations");
            }

            if (step.Parameters.TryGetValue("optimization_level", out var level))
            {
                rollback.Add($"restore_optimization_level:{level}");
            }

            return string.Join(";", rollback);
        }

        private string GenerateResourceUsageRollback(OptimizationStep step, Dictionary<string, object> states)
        {
            var rollback = new List<string>();

            if (step.Parameters.TryGetValue("target_efficiency", out var target))
            {
                rollback.Add($"restore_efficiency_target:{target}");
                rollback.Add("revert_resource_allocation");
            }

            if (step.Parameters.TryGetValue("resource_limits", out var limits))
            {
                rollback.Add($"restore_resource_limits:{limits}");
            }

            return string.Join(";", rollback);
        }

        private string GenerateDefaultRollback(OptimizationStep step)
        {
            var rollback = new List<string>
    {
        $"revert_{step.Action}",
        "verify_resource_state",
        "restore_default_configuration"
    };

            return string.Join(";", rollback);
        }

        private string GenerateVerificationStep(OptimizationStep step)
        {
            return step.ValidationChecks != null && step.ValidationChecks.Any()
                ? $"verify_rollback:action={step.Action};checks={string.Join(",", step.ValidationChecks)}"
                : $"verify_rollback:action={step.Action}";
        }

        private string FormatRollbackProcedure(List<string> steps)
        {
            // Add metadata
            var procedure = new List<string>
    {
        $"# Rollback Procedure Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
        $"# Total Steps: {steps.Count}",
        "# Execute steps in sequence. Stop and alert on any failure.",
        "",
        "BEGIN_ROLLBACK"
    };

            // Add numbered steps
            for (int i = 0; i < steps.Count; i++)
            {
                procedure.Add($"{i + 1}. {steps[i]}");
            }

            procedure.Add("END_ROLLBACK");

            return string.Join(Environment.NewLine, procedure);
        }
        /// <summary>
        /// Validates the optimization plan against constraints and ensures it meets all requirements
        /// </summary>
        private async Task ValidateOptimizationPlanAsync(OptimizationPlan plan, OptimizationConstraints constraints)
        {
            try
            {
                _logger.LogInformation($"Validating optimization plan for resource: {plan.ResourceId}");

                // Validate basic plan structure
                ValidatePlanStructure(plan);

                // Validate each step
                foreach (var step in plan.Steps)
                {
                    await ValidateOptimizationStepAsync(step, constraints);
                }

                // Validate expected improvements against constraints
                ValidateExpectedImprovements(plan.ExpectedImprovements, constraints);

                // Validate validation criteria
                ValidateValidationCriteria(plan.ValidationCriteria, constraints);

                // Validate rollback procedure
                ValidateRollbackProcedure(plan.RollbackProcedure);

                // Validate overall impact and resource constraints
                await ValidateResourceImpactAsync(plan, constraints);

                _logger.LogInformation("Optimization plan validation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating optimization plan");
                throw new InvalidOperationException($"Plan validation failed: {ex.Message}", ex);
            }
        }

        private void ValidatePlanStructure(OptimizationPlan plan)
        {
            if (string.IsNullOrEmpty(plan.ResourceId))
            {
                throw new ArgumentException("Plan must have a resource ID");
            }

            if (plan.Steps == null || !plan.Steps.Any())
            {
                throw new ArgumentException("Plan must contain at least one optimization step");
            }

            if (plan.ExpectedImprovements == null)
            {
                throw new ArgumentException("Plan must specify expected improvements");
            }

            if (plan.ValidationCriteria == null)
            {
                throw new ArgumentException("Plan must specify validation criteria");
            }

            if (string.IsNullOrEmpty(plan.RollbackProcedure))
            {
                throw new ArgumentException("Plan must include rollback procedure");
            }
        }

        private async Task ValidateOptimizationStepAsync(OptimizationStep step, OptimizationConstraints constraints)
        {
            // Validate step action
            if (string.IsNullOrEmpty(step.Action))
            {
                throw new ArgumentException("Step must specify an action");
            }

            // Validate parameters based on action type
            switch (step.Action)
            {
                case "optimize_utilization":
                    ValidateUtilizationParameters(step, constraints);
                    break;

                case "optimize_performance":
                    ValidatePerformanceParameters(step, constraints);
                    break;

                case "optimize_resource_usage":
                    ValidateResourceUsageParameters(step, constraints);
                    break;

                default:
                    throw new ArgumentException($"Unknown optimization action: {step.Action}");
            }

            // Validate expected impact
            if (step.ExpectedImpact == null || !step.ExpectedImpact.Any())
            {
                throw new ArgumentException($"Step {step.Action} must specify expected impact");
            }

            // Validate impact values are within constraints
            foreach (var (metric, impact) in step.ExpectedImpact)
            {
                if (constraints.ResourceLimits.TryGetValue($"max_{metric}", out var limit))
                {
                    if (impact > limit)
                    {
                        throw new InvalidOperationException(
                            $"Step {step.Action} exceeds {metric} limit: {impact:F2} > {limit:F2}");
                    }
                }
            }

            // Validate validation checks
            if (step.ValidationChecks == null || !step.ValidationChecks.Any())
            {
                throw new ArgumentException($"Step {step.Action} must specify validation checks");
            }

            // Validate rollback action exists
            if (string.IsNullOrEmpty(step.RollbackAction))
            {
                throw new ArgumentException($"Step {step.Action} must specify rollback action");
            }
        }

        private void ValidateUtilizationParameters(OptimizationStep step, OptimizationConstraints constraints)
        {
            if (!step.Parameters.ContainsKey("target_utilization"))
            {
                throw new ArgumentException("Utilization optimization must specify target_utilization");
            }

            var targetUtilization = Convert.ToDouble(step.Parameters["target_utilization"]);
            if (targetUtilization > constraints.ResourceLimits.GetValueOrDefault("max_utilization", 0.9))
            {
                throw new InvalidOperationException("Target utilization exceeds maximum limit");
            }
        }

        private void ValidatePerformanceParameters(OptimizationStep step, OptimizationConstraints constraints)
        {
            if (!step.Parameters.ContainsKey("target_performance"))
            {
                throw new ArgumentException("Performance optimization must specify target_performance");
            }

            var targetPerformance = Convert.ToDouble(step.Parameters["target_performance"]);
            if (targetPerformance < constraints.PerformanceTargets.GetValueOrDefault("min_performance", 0.8))
            {
                throw new InvalidOperationException("Target performance below minimum requirement");
            }
        }

        private void ValidateResourceUsageParameters(OptimizationStep step, OptimizationConstraints constraints)
        {
            if (!step.Parameters.ContainsKey("target_efficiency"))
            {
                throw new ArgumentException("Resource usage optimization must specify target_efficiency");
            }

            var targetEfficiency = Convert.ToDouble(step.Parameters["target_efficiency"]);
            if (targetEfficiency > constraints.ResourceLimits.GetValueOrDefault("max_efficiency", 0.95))
            {
                throw new InvalidOperationException("Target efficiency exceeds maximum limit");
            }
        }

        private void ValidateExpectedImprovements(Dictionary<string, double> improvements, OptimizationConstraints constraints)
        {
            if (improvements == null || !improvements.Any())
            {
                throw new ArgumentException("Expected improvements cannot be empty");
            }

            foreach (var (metric, value) in improvements)
            {
                // Check against resource limits
                if (constraints.ResourceLimits.TryGetValue($"max_{metric}", out var limit))
                {
                    if (value > limit)
                    {
                        throw new InvalidOperationException(
                            $"Expected improvement for {metric} exceeds limit: {value:F2} > {limit:F2}");
                    }
                }

                // Check against performance targets
                if (constraints.PerformanceTargets.TryGetValue($"min_{metric}", out var target))
                {
                    if (value < target)
                    {
                        throw new InvalidOperationException(
                            $"Expected improvement for {metric} below target: {value:F2} < {target:F2}");
                    }
                }
            }
        }

        private void ValidateValidationCriteria(Dictionary<string, object> criteria, OptimizationConstraints constraints)
        {
            if (criteria == null || !criteria.Any())
            {
                throw new ArgumentException("Validation criteria cannot be empty");
            }

            // Ensure required criteria are present
            var requiredCriteria = new[]
            {
        "min_performance",
        "max_error_rate",
        "stability_threshold"
    };

            foreach (var required in requiredCriteria)
            {
                if (!criteria.ContainsKey(required))
                {
                    throw new ArgumentException($"Missing required validation criterion: {required}");
                }
            }

            // Validate criteria values against constraints
            foreach (var (key, value) in criteria)
            {
                if (key.StartsWith("resource_limit_") &&
                    constraints.ResourceLimits.TryGetValue(key.Substring("resource_limit_".Length), out var limit))
                {
                    var criteriaValue = Convert.ToDouble(value);
                    if (criteriaValue > limit)
                    {
                        throw new InvalidOperationException(
                            $"Validation criterion {key} exceeds resource limit: {criteriaValue:F2} > {limit:F2}");
                    }
                }
            }
        }

        private void ValidateRollbackProcedure(string rollbackProcedure)
        {
            if (string.IsNullOrEmpty(rollbackProcedure))
            {
                throw new ArgumentException("Rollback procedure cannot be empty");
            }

            // Ensure rollback procedure has required sections
            if (!rollbackProcedure.Contains("BEGIN_ROLLBACK") ||
                !rollbackProcedure.Contains("END_ROLLBACK"))
            {
                throw new ArgumentException("Invalid rollback procedure format");
            }

            // Validate rollback steps
            var steps = rollbackProcedure
                .Split(Environment.NewLine)
                .Where(line => line.StartsWith("1.") || line.StartsWith("2."))
                .ToList();

            if (!steps.Any())
            {
                throw new ArgumentException("Rollback procedure must contain numbered steps");
            }
        }

        private async Task ValidateResourceImpactAsync(OptimizationPlan plan, OptimizationConstraints constraints)
        {
            // Get current resource status
            var tracker = await GetResourceStatusAsync(plan.ResourceId);
            if (tracker == null)
            {
                throw new InvalidOperationException($"Resource not found: {plan.ResourceId}");
            }

            // Calculate total resource impact
            var totalImpact = new Dictionary<string, double>();
            foreach (var step in plan.Steps)
            {
                foreach (var (metric, impact) in step.ExpectedImpact)
                {
                    if (totalImpact.ContainsKey(metric))
                    {
                        totalImpact[metric] += impact;
                    }
                    else
                    {
                        totalImpact[metric] = impact;
                    }
                }
            }

            // Validate total impact against resource metrics
            foreach (var (metric, impact) in totalImpact)
            {
                var currentValue = tracker.Metrics.GetValueOrDefault(metric, 0.0);
                var projectedValue = currentValue * (1 + impact);

                if (constraints.ResourceLimits.TryGetValue($"max_{metric}", out var limit) &&
                    projectedValue > limit)
                {
                    throw new InvalidOperationException(
                        $"Total impact on {metric} would exceed resource limit: {projectedValue:F2} > {limit:F2}");
                }
            }
        }
        /// <summary>
        /// Validates a specific criterion against resource tracker values
        /// </summary>
        private async Task<bool> ValidateCriterionAsync(ResourceTracker tracker, string criterion, object expectedValue)
        {
            try
            {
                _logger.LogDebug($"Validating criterion: {criterion} with expected value: {expectedValue}");

                // Handle different criterion types
                switch (criterion)
                {
                    case "min_performance":
                        return await ValidatePerformanceCriterionAsync(tracker, expectedValue);

                    case "max_error_rate":
                        return await ValidateErrorRateCriterionAsync(tracker, expectedValue);

                    case "stability_threshold":
                        return await ValidateStabilityCriterionAsync(tracker, expectedValue);

                    case "resource_usage":
                        return await ValidateResourceUsageCriterionAsync(tracker, expectedValue);

                    case "efficiency":
                        return await ValidateEfficiencyCriterionAsync(tracker, expectedValue);

                    case var c when c.StartsWith("resource_limit_"):
                        return ValidateResourceLimitCriterion(tracker, criterion.Substring("resource_limit_".Length), expectedValue);

                    case var c when c.StartsWith("performance_target_"):
                        return ValidatePerformanceTargetCriterion(tracker, criterion.Substring("performance_target_".Length), expectedValue);

                    default:
                        return await ValidateGenericCriterionAsync(tracker, criterion, expectedValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating criterion: {criterion}");
                throw;
            }
        }

        private async Task<bool> ValidatePerformanceCriterionAsync(ResourceTracker tracker, object expectedValue)
        {
            var currentPerformance = tracker.Metrics.GetValueOrDefault("performance", 0.0);
            var minimumRequired = Convert.ToDouble(expectedValue);

            if (currentPerformance < minimumRequired)
            {
                _logger.LogWarning($"Performance below minimum requirement: {currentPerformance:F2} < {minimumRequired:F2}");
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateErrorRateCriterionAsync(ResourceTracker tracker, object expectedValue)
        {
            var currentErrorRate = tracker.Metrics.GetValueOrDefault("error_rate", 0.0);
            var maximumAllowed = Convert.ToDouble(expectedValue);

            if (currentErrorRate > maximumAllowed)
            {
                _logger.LogWarning($"Error rate exceeds maximum: {currentErrorRate:F2} > {maximumAllowed:F2}");
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateStabilityCriterionAsync(ResourceTracker tracker, object expectedValue)
        {
            // Get stability metrics over time window
            var stabilityMetrics = await GetStabilityMetricsAsync(tracker);
            var requiredStability = Convert.ToDouble(expectedValue);

            var stabilityScore = CalculateStabilityScore(stabilityMetrics);

            if (stabilityScore < requiredStability)
            {
                _logger.LogWarning($"Stability below threshold: {stabilityScore:F2} < {requiredStability:F2}");
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateResourceUsageCriterionAsync(ResourceTracker tracker, object expectedValue)
        {
            var currentUsage = tracker.Metrics.GetValueOrDefault("utilization", 0.0);
            var maximumAllowed = Convert.ToDouble(expectedValue);

            if (currentUsage > maximumAllowed)
            {
                _logger.LogWarning($"Resource usage exceeds maximum: {currentUsage:F2} > {maximumAllowed:F2}");
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateEfficiencyCriterionAsync(ResourceTracker tracker, object expectedValue)
        {
            var currentEfficiency = tracker.Metrics.GetValueOrDefault("efficiency", 0.0);
            var minimumRequired = Convert.ToDouble(expectedValue);

            if (currentEfficiency < minimumRequired)
            {
                _logger.LogWarning($"Efficiency below requirement: {currentEfficiency:F2} < {minimumRequired:F2}");
                return false;
            }

            return true;
        }

        private bool ValidateResourceLimitCriterion(ResourceTracker tracker, string resource, object expectedValue)
        {
            var metricKey = $"resource_{resource}";
            if (!tracker.Metrics.TryGetValue(metricKey, out var currentValue))
            {
                _logger.LogWarning($"Resource metric not found: {metricKey}");
                return false;
            }

            var limit = Convert.ToDouble(expectedValue);
            if (currentValue > limit)
            {
                _logger.LogWarning($"Resource {resource} exceeds limit: {currentValue:F2} > {limit:F2}");
                return false;
            }

            return true;
        }

        private bool ValidatePerformanceTargetCriterion(ResourceTracker tracker, string metric, object expectedValue)
        {
            if (!tracker.Metrics.TryGetValue(metric, out var currentValue))
            {
                _logger.LogWarning($"Performance metric not found: {metric}");
                return false;
            }

            var target = Convert.ToDouble(expectedValue);
            if (currentValue < target)
            {
                _logger.LogWarning($"Performance metric {metric} below target: {currentValue:F2} < {target:F2}");
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateGenericCriterionAsync(ResourceTracker tracker, string criterion, object expectedValue)
        {
            // Get current value from metrics or metadata
            object currentValue = null;

            if (tracker.Metrics.TryGetValue(criterion, out var metricValue))
            {
                currentValue = metricValue;
            }
            else if (tracker.Metadata.TryGetValue(criterion, out var metadataValue))
            {
                currentValue = metadataValue;
            }
            else
            {
                _logger.LogWarning($"Criterion not found in metrics or metadata: {criterion}");
                return false;
            }

            // Compare based on type
            if (currentValue is double numericValue && expectedValue is double expectedNumeric)
            {
                return numericValue >= expectedNumeric;
            }

            if (currentValue is bool boolValue && expectedValue is bool expectedBool)
            {
                return boolValue == expectedBool;
            }

            // String comparison
            return currentValue.ToString().Equals(
                expectedValue.ToString(),
                StringComparison.OrdinalIgnoreCase
            );
        }

        private async Task<List<Dictionary<string, double>>> GetStabilityMetricsAsync(ResourceTracker tracker)
        {
            // This would typically get metrics from a time series database or monitoring system
            // For now, we'll simulate with recent metrics
            var metrics = new List<Dictionary<string, double>>
    {
        tracker.Metrics,
        // Add historical metrics if available
    };

            return metrics;
        }

        private double CalculateStabilityScore(List<Dictionary<string, double>> metrics)
        {
            // Calculate stability based on metric variance
            // This is a simplified implementation
            if (!metrics.Any()) return 0;

            var stabilityFactors = new List<double>();

            // Check key metrics variance
            var keyMetrics = new[] { "performance", "error_rate", "utilization" };
            foreach (var metric in keyMetrics)
            {
                var values = metrics
                    .Where(m => m.ContainsKey(metric))
                    .Select(m => m[metric])
                    .ToList();

                if (values.Any())
                {
                    var variance = CalculateVariance(values);
                    var stabilityFactor = Math.Max(0, 1 - variance);
                    stabilityFactors.Add(stabilityFactor);
                }
            }

            return stabilityFactors.Any() ?
                stabilityFactors.Average() :
                0;
        }

        private double CalculateVariance(List<double> values)
        {
            if (!values.Any()) return 0;

            var mean = values.Average();
            var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquaredDiffs / values.Count) / mean; // Coefficient of variation
        }
    }


}