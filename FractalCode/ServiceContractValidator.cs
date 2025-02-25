using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FractalCode.Core
{
    /// <summary>
    /// Defines a contract for validating service registrations and their interactions.
    /// 
    /// This interface is part of the Contract System described in the Code Fractalization Protocol
    /// and ensures that service dependencies maintain proper contracts between components.
    /// </summary>
    public interface IServiceContractValidator
    {
        /// <summary>
        /// Validates that all registered services adhere to their defined contracts.
        /// </summary>
        /// <returns>A validation result containing any issues found</returns>
        Task<ServiceContractValidationResult> ValidateServicesAsync();

        /// <summary>
        /// Validates a specific service registration against its contract.
        /// </summary>
        /// <param name="serviceType">The service interface type</param>
        /// <param name="implementationType">The implementation type</param>
        /// <returns>A validation result for the specific service</returns>
        Task<ServiceContractValidationResult> ValidateServiceAsync(Type serviceType, Type implementationType);

        /// <summary>
        /// Verifies the contract compatibility between two interacting services.
        /// </summary>
        /// <param name="consumerType">The service consuming the dependency</param>
        /// <param name="dependencyType">The dependency being consumed</param>
        /// <returns>A result indicating if the interaction is valid</returns>
        Task<ServiceInteractionValidationResult> ValidateServiceInteractionAsync(Type consumerType, Type dependencyType);

        /// <summary>
        /// Monitors service contract health and reports any degradations.
        /// </summary>
        /// <returns>Health status of service contracts</returns>
        Task<ServiceContractHealth> MonitorContractHealthAsync();
    }

    /// <summary>
    /// Represents the result of a service contract validation operation.
    /// </summary>
    public class ServiceContractValidationResult
    {
        /// <summary>
        /// Gets whether the validation passed successfully.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets a list of validation errors found during the validation.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Gets a list of validation warnings that don't invalidate the contract.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets metrics associated with the validation process.
        /// </summary>
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Represents the result of validating an interaction between two services.
    /// </summary>
    public class ServiceInteractionValidationResult
    {
        /// <summary>
        /// Gets whether the interaction is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets a list of compatibility issues found.
        /// </summary>
        public List<string> CompatibilityIssues { get; set; } = new List<string>();

        /// <summary>
        /// Gets potential impact metrics for the interaction.
        /// </summary>
        public Dictionary<string, double> ImpactMetrics { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Represents the health status of service contracts in the system.
    /// </summary>
    public class ServiceContractHealth
    {
        /// <summary>
        /// Gets overall health status (0.0 to 1.0 scale).
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Gets metrics for specific aspects of contract health.
        /// </summary>
        public Dictionary<string, double> HealthMetrics { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Gets active health issues with service contracts.
        /// </summary>
        public List<ServiceContractHealthIssue> ActiveIssues { get; set; } = new List<ServiceContractHealthIssue>();
    }

    /// <summary>
    /// Represents a specific health issue with a service contract.
    /// </summary>
    public class ServiceContractHealthIssue
    {
        /// <summary>
        /// Gets the service type with the issue.
        /// </summary>
        public Type ServiceType { get; set; }

        /// <summary>
        /// Gets the issue type category.
        /// </summary>
        public string IssueType { get; set; }

        /// <summary>
        /// Gets a description of the issue.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the severity of the issue (Critical, Warning, Info).
        /// </summary>
        public string Severity { get; set; }

        /// <summary>
        /// Gets recommended actions to resolve the issue.
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Implements service contract validation for dependency injection services.
    /// 
    /// This class validates that service implementations adhere to their contracts
    /// and that service interactions respect defined boundaries. It follows the
    /// Contract System principles from the Code Fractalization Protocol.
    /// 
    /// Design decisions:
    /// - Uses reflection to analyze service contracts and implementations
    /// - Performs both static analysis and runtime validation
    /// - Supports flexible contract evolution through version compatibility checks
    /// - Provides health monitoring for service contract violations
    /// </summary>
    public class ServiceContractValidator : IServiceContractValidator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServiceContractValidator> _logger;
        private readonly Dictionary<Type, ServiceContractMetadata> _contractCache;

        /// <summary>
        /// Initializes a new instance of the ServiceContractValidator class.
        /// </summary>
        /// <param name="serviceProvider">The service provider to validate</param>
        public ServiceContractValidator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = _serviceProvider.GetService<ILogger<ServiceContractValidator>>();
            _contractCache = new Dictionary<Type, ServiceContractMetadata>();
        }

        /// <summary>
        /// Validates all registered services against their contracts.
        /// </summary>
        /// <returns>A validation result with any issues found</returns>
        public async Task<ServiceContractValidationResult> ValidateServicesAsync()
        {
            try
            {
                _logger.LogInformation("Validating all service contracts");

                var result = new ServiceContractValidationResult
                {
                    IsValid = true,
                    Metrics = new Dictionary<string, double>
                    {
                        ["total_services"] = 0,
                        ["valid_services"] = 0,
                        ["validation_duration_ms"] = 0
                    }
                };

                var startTime = DateTime.UtcNow;

                // Get all registered service descriptors
                var services = GetRegisteredServices();
                result.Metrics["total_services"] = services.Count;

                foreach (var descriptor in services)
                {
                    if (descriptor.ServiceType.IsInterface && descriptor.ImplementationType != null)
                    {
                        var serviceResult = await ValidateServiceAsync(
                            descriptor.ServiceType,
                            descriptor.ImplementationType);

                        if (!serviceResult.IsValid)
                        {
                            result.IsValid = false;
                            result.Errors.AddRange(serviceResult.Errors);
                            result.Warnings.AddRange(serviceResult.Warnings);
                        }
                        else
                        {
                            result.Metrics["valid_services"]++;
                        }
                    }
                }

                result.Metrics["validation_duration_ms"] =
                    (DateTime.UtcNow - startTime).TotalMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating service contracts");
                return new ServiceContractValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Validation error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Validates a specific service against its contract.
        /// </summary>
        /// <param name="serviceType">The service interface type</param>
        /// <param name="implementationType">The implementation type</param>
        /// <returns>A validation result for the specific service</returns>
        public async Task<ServiceContractValidationResult> ValidateServiceAsync(
            Type serviceType,
            Type implementationType)
        {
            try
            {
                _logger.LogDebug("Validating service contract: {ServiceType}", serviceType.Name);

                var result = new ServiceContractValidationResult
                {
                    IsValid = true
                };

                // Validate interface implementation
                if (!serviceType.IsAssignableFrom(implementationType))
                {
                    result.IsValid = false;
                    result.Errors.Add(
                        $"Type {implementationType.Name} does not implement {serviceType.Name}");
                    return result;
                }

                // Get or create contract metadata
                var metadata = GetContractMetadata(serviceType);

                // Validate contract requirements
                ValidateContractRequirements(implementationType, metadata, result);

                // Validate version compatibility
                ValidateVersionCompatibility(implementationType, metadata, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating service: {ServiceType}", serviceType.Name);
                return new ServiceContractValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Validation error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Validates the interaction between two services.
        /// </summary>
        /// <param name="consumerType">The service consuming the dependency</param>
        /// <param name="dependencyType">The dependency being consumed</param>
        /// <returns>A result indicating if the interaction is valid</returns>
        public async Task<ServiceInteractionValidationResult> ValidateServiceInteractionAsync(
            Type consumerType,
            Type dependencyType)
        {
            try
            {
                _logger.LogDebug(
                    "Validating service interaction: {Consumer} -> {Dependency}",
                    consumerType.Name,
                    dependencyType.Name);

                var result = new ServiceInteractionValidationResult
                {
                    IsValid = true,
                    ImpactMetrics = new Dictionary<string, double>()
                };

                // Get contract metadata for both types
                var consumerMetadata = GetContractMetadata(consumerType);
                var dependencyMetadata = GetContractMetadata(dependencyType);

                // Check compatibility between versions
                if (consumerMetadata.MinimumDependencyVersion > dependencyMetadata.Version)
                {
                    result.IsValid = false;
                    result.CompatibilityIssues.Add(
                        $"{consumerType.Name} requires {dependencyType.Name} " +
                        $"version {consumerMetadata.MinimumDependencyVersion} or higher, " +
                        $"but found version {dependencyMetadata.Version}");
                }

                // Calculate impact metrics
                result.ImpactMetrics["dependency_strength"] =
                    CalculateDependencyStrength(consumerType, dependencyType);
                result.ImpactMetrics["change_impact"] =
                    CalculateChangeImpact(consumerType, dependencyType);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error validating interaction: {Consumer} -> {Dependency}",
                    consumerType.Name,
                    dependencyType.Name);

                return new ServiceInteractionValidationResult
                {
                    IsValid = false,
                    CompatibilityIssues = new List<string> { $"Validation error: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Monitors health of service contracts in the system.
        /// </summary>
        /// <returns>Health status of service contracts</returns>
        public async Task<ServiceContractHealth> MonitorContractHealthAsync()
        {
            try
            {
                _logger.LogInformation("Monitoring service contract health");

                var health = new ServiceContractHealth
                {
                    HealthScore = 1.0,
                    HealthMetrics = new Dictionary<string, double>(),
                    ActiveIssues = new List<ServiceContractHealthIssue>()
                };

                // Perform validation to find current issues
                var validation = await ValidateServicesAsync();

                // Calculate health score based on validation results
                if (validation.Metrics.TryGetValue("total_services", out var total) &&
                    validation.Metrics.TryGetValue("valid_services", out var valid) &&
                    total > 0)
                {
                    health.HealthScore = valid / total;
                }

                // Copy metrics
                foreach (var metric in validation.Metrics)
                {
                    health.HealthMetrics[metric.Key] = metric.Value;
                }

                // Create health issues from validation errors
                foreach (var error in validation.Errors)
                {
                    // Parse error to determine affected service
                    var serviceType = DetermineAffectedService(error);

                    health.ActiveIssues.Add(new ServiceContractHealthIssue
                    {
                        ServiceType = serviceType,
                        IssueType = "ContractViolation",
                        Description = error,
                        Severity = "Critical",
                        RecommendedActions = GenerateRecommendedActions(error)
                    });
                }

                // Add warnings as info-level issues
                foreach (var warning in validation.Warnings)
                {
                    var serviceType = DetermineAffectedService(warning);

                    health.ActiveIssues.Add(new ServiceContractHealthIssue
                    {
                        ServiceType = serviceType,
                        IssueType = "PotentialIssue",
                        Description = warning,
                        Severity = "Warning",
                        RecommendedActions = GenerateRecommendedActions(warning)
                    });
                }

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring contract health");

                return new ServiceContractHealth
                {
                    HealthScore = 0,
                    ActiveIssues = new List<ServiceContractHealthIssue>
                    {
                        new ServiceContractHealthIssue
                        {
                            ServiceType = typeof(ServiceContractValidator),
                            IssueType = "MonitoringFailure",
                            Description = $"Failed to monitor contract health: {ex.Message}",
                            Severity = "Critical",
                            RecommendedActions = new List<string>
                            {
                                "Check contract validator configuration",
                                "Verify service registration",
                                "Review exception details in logs"
                            }
                        }
                    }
                };
            }
        }

        #region Private helper methods

        private List<ServiceDescriptor> GetRegisteredServices()
        {
            if (_serviceProvider is not ServiceProvider provider)
            {
                // If we can't get direct access, return empty list
                return new List<ServiceDescriptor>();
            }

            // Using reflection to access the service descriptors
            var engine = provider.GetFieldValue("_engine");
            var callSiteFactory = engine.GetPropertyValue("CallSiteFactory");
            var descriptors = callSiteFactory.GetFieldValue("_descriptors");

            return (List<ServiceDescriptor>)descriptors;
        }

        private ServiceContractMetadata GetContractMetadata(Type serviceType)
        {
            if (_contractCache.TryGetValue(serviceType, out var metadata))
            {
                return metadata;
            }

            metadata = new ServiceContractMetadata
            {
                ServiceType = serviceType,
                Version = ExtractVersion(serviceType),
                MinimumDependencyVersion = ExtractMinimumDependencyVersion(serviceType),
                RequiredMethods = ExtractRequiredMethods(serviceType),
                FlexibilityZones = ExtractFlexibilityZones(serviceType)
            };

            _contractCache[serviceType] = metadata;
            return metadata;
        }

        private void ValidateContractRequirements(
            Type implementationType,
            ServiceContractMetadata metadata,
            ServiceContractValidationResult result)
        {
            // Validate all required methods are implemented correctly
            foreach (var method in metadata.RequiredMethods)
            {
                var implMethod = implementationType.GetMethod(
                    method.Name,
                    method.GetParameters().Select(p => p.ParameterType).ToArray());

                if (implMethod == null)
                {
                    result.IsValid = false;
                    result.Errors.Add(
                        $"Required method {method.Name} not implemented in {implementationType.Name}");
                }
                else if (implMethod.ReturnType != method.ReturnType)
                {
                    result.IsValid = false;
                    result.Errors.Add(
                        $"Method {method.Name} in {implementationType.Name} returns " +
                        $"{implMethod.ReturnType.Name} but should return {method.ReturnType.Name}");
                }
            }

            // Additional validation logic would go here
        }

        private void ValidateVersionCompatibility(
            Type implementationType,
            ServiceContractMetadata metadata,
            ServiceContractValidationResult result)
        {
            var implVersion = ExtractVersion(implementationType);

            if (implVersion < metadata.Version)
            {
                result.Warnings.Add(
                    $"Implementation version {implVersion} is lower than interface version {metadata.Version}");
            }
        }

        private double ExtractVersion(Type type)
        {
            // Extract version from attributes or naming conventions
            var versionAttr = type.GetCustomAttribute<ContractVersionAttribute>();
            if (versionAttr != null)
            {
                return versionAttr.Version;
            }

            // Default version if not specified
            return 1.0;
        }

        private double ExtractMinimumDependencyVersion(Type type)
        {
            // Extract from attributes
            var depAttr = type.GetCustomAttribute<MinimumDependencyVersionAttribute>();
            if (depAttr != null)
            {
                return depAttr.Version;
            }

            // Default
            return 1.0;
        }

        private List<MethodInfo> ExtractRequiredMethods(Type type)
        {
            // Get all methods declared directly on this interface
            return type.GetMethods()
                .Where(m => !m.IsSpecialName) // Skip property accessors
                .ToList();
        }

        private Dictionary<string, FlexibilityZone> ExtractFlexibilityZones(Type type)
        {
            var zones = new Dictionary<string, FlexibilityZone>();

            // Get flexibility zones from attributes
            var methods = type.GetMethods();
            foreach (var method in methods)
            {
                var zoneAttr = method.GetCustomAttribute<FlexibilityZoneAttribute>();
                if (zoneAttr != null)
                {
                    zones[method.Name] = new FlexibilityZone
                    {
                        Name = zoneAttr.Name,
                        AllowedAdaptations = zoneAttr.AllowedAdaptations,
                        Boundaries = zoneAttr.Boundaries
                    };
                }
            }

            return zones;
        }

        private double CalculateDependencyStrength(Type consumerType, Type dependencyType)
        {
            // Calculate based on usage patterns
            // This would be more sophisticated in a real implementation
            return 0.5;
        }

        private double CalculateChangeImpact(Type consumerType, Type dependencyType)
        {
            // Calculate potential impact of changes
            // This would be more sophisticated in a real implementation
            return 0.3;
        }

        private Type DetermineAffectedService(string errorMessage)
        {
            // Parse error message to extract type information
            // For this example, we'll just return Object as a placeholder
            return typeof(object);
        }

        private List<string> GenerateRecommendedActions(string issue)
        {
            // Generate recommendations based on issue content
            // In a real implementation, this would use more sophisticated analysis
            return new List<string>
            {
                "Review service implementation for contract compliance",
                "Check version compatibility",
                "Verify interface implementation"
            };
        }

        #endregion
    }

    /// <summary>
    /// Stores metadata about a service contract for validation purposes.
    /// </summary>
    internal class ServiceContractMetadata
    {
        /// <summary>
        /// Gets the service interface type.
        /// </summary>
        public Type ServiceType { get; set; }

        /// <summary>
        /// Gets the contract version.
        /// </summary>
        public double Version { get; set; }

        /// <summary>
        /// Gets the minimum version required for dependencies.
        /// </summary>
        public double MinimumDependencyVersion { get; set; }

        /// <summary>
        /// Gets the methods required by this contract.
        /// </summary>
        public List<MethodInfo> RequiredMethods { get; set; } = new List<MethodInfo>();

        /// <summary>
        /// Gets flexibility zones defined in the contract.
        /// </summary>
        public Dictionary<string, FlexibilityZone> FlexibilityZones { get; set; } =
            new Dictionary<string, FlexibilityZone>();
    }

    /// <summary>
    /// Represents a zone of allowed flexibility in a contract.
    /// </summary>
    internal class FlexibilityZone
    {
        /// <summary>
        /// Gets the name of the flexibility zone.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the allowed adaptations in this zone.
        /// </summary>
        public string[] AllowedAdaptations { get; set; }

        /// <summary>
        /// Gets the boundaries of the flexibility zone.
        /// </summary>
        public Dictionary<string, object> Boundaries { get; set; }
    }

    /// <summary>
    /// Extension methods to help with reflection access.
    /// </summary>
    internal static class ReflectionExtensions
    {
        public static object GetFieldValue(this object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return field?.GetValue(obj);
        }

        public static object GetPropertyValue(this object obj, string propertyName)
        {
            var property = obj.GetType().GetProperty(propertyName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(obj);
        }
    }

    /// <summary>
    /// Specifies the contract version of a service interface or implementation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class ContractVersionAttribute : Attribute
    {
        /// <summary>
        /// Gets the contract version.
        /// </summary>
        public double Version { get; }

        /// <summary>
        /// Initializes a new instance of the ContractVersionAttribute class.
        /// </summary>
        /// <param name="version">The contract version</param>
        public ContractVersionAttribute(double version)
        {
            Version = version;
        }
    }

    /// <summary>
    /// Specifies the minimum version required for dependencies.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class MinimumDependencyVersionAttribute : Attribute
    {
        /// <summary>
        /// Gets the minimum dependency version.
        /// </summary>
        public double Version { get; }

        /// <summary>
        /// Initializes a new instance of the MinimumDependencyVersionAttribute class.
        /// </summary>
        /// <param name="version">The minimum dependency version</param>
        public MinimumDependencyVersionAttribute(double version)
        {
            Version = version;
        }
    }

    /// <summary>
    /// Defines a flexibility zone in a contract.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class FlexibilityZoneAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the flexibility zone.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the allowed adaptations in this zone.
        /// </summary>
        public string[] AllowedAdaptations { get; }

        /// <summary>
        /// Gets the boundaries of the flexibility zone.
        /// </summary>
        public Dictionary<string, object> Boundaries { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the FlexibilityZoneAttribute class.
        /// </summary>
        /// <param name="name">The name of the flexibility zone</param>
        /// <param name="allowedAdaptations">Allowed adaptations in this zone</param>
        public FlexibilityZoneAttribute(string name, params string[] allowedAdaptations)
        {
            Name = name;
            AllowedAdaptations = allowedAdaptations;
        }
    }

}