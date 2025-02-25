using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FractalCode.Core.Models;
using Microsoft.Extensions.Logging;

namespace FractalCode.Core
{
    /// <summary>
    /// Interface for tracking and managing resources throughout their lifecycle.
    /// 
    /// Follows the Code Fractalization Protocol by:
    /// - Providing clear contract boundaries for resource tracking
    /// - Supporting the Knowledge Layer through context preservation
    /// - Enabling monitoring as part of the Implementation Layer
    /// </summary>
    public interface IResourceTracker
    {
        /// <summary>
        /// Tracks a new resource within the system
        /// </summary>
        /// <param name="resource">The resource to track</param>
        /// <param name="context">The fractal context in which the resource exists</param>
        /// <returns>A ResourceTracker instance with tracking details</returns>
        Task<ResourceTracker> TrackResourceAsync(Resource resource, FractalContext context);

        /// <summary>
        /// Gets the current status of a tracked resource
        /// </summary>
        /// <param name="resourceId">The ID of the resource to check</param>
        /// <returns>The resource tracker with current status, or null if not found</returns>
        Task<ResourceTracker> GetResourceStatusAsync(string resourceId);

        /// <summary>
        /// Updates resource metadata and tracking information
        /// </summary>
        /// <param name="resourceId">The ID of the resource to update</param>
        /// <param name="updates">The updates to apply to the resource</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        Task<bool> UpdateResourceAsync(string resourceId, Dictionary<string, object> updates);

        /// <summary>
        /// Validates a resource against specified criteria
        /// </summary>
        /// <param name="resourceId">The ID of the resource to validate</param>
        /// <param name="criteria">The validation criteria</param>
        /// <returns>True if validation passes, false otherwise</returns>
        Task<bool> ValidateResourceAsync(string resourceId, Dictionary<string, object> criteria);

        /// <summary>
        /// Gets all resources matching specified criteria
        /// </summary>
        /// <param name="filter">Optional filter criteria</param>
        /// <returns>Collection of resources matching the filter</returns>
        Task<IEnumerable<ResourceTracker>> GetResourcesAsync(Dictionary<string, object> filter = null);

        /// <summary>
        /// Gets the health metrics for a resource
        /// </summary>
        /// <param name="resourceId">The ID of the resource</param>
        /// <returns>Dictionary of health metrics</returns>
        Task<Dictionary<string, double>> GetResourceHealthMetricsAsync(string resourceId);
    }

    /// <summary>
    /// Tracks and manages resources throughout their lifecycle.
    /// 
    /// This class follows the Code Fractalization Protocol by:
    /// - Preserving resource context over time
    /// - Implementing clear responsibility boundaries
    /// - Supporting validation and health monitoring
    /// - Maintaining historical data for analysis
    /// 
    /// Evolution History:
    /// - Initial implementation as data model only (v1.0)
    /// - Enhanced with lifecycle management capabilities (v1.1)
    /// - Added health monitoring and metrics collection (v1.2)
    /// - Integrated with contract validation system (v1.3)
    /// </summary>
    public class ResourceTracker : IResourceTracker
    {
        private readonly ILogger<ResourceTracker> _logger;
        private readonly IResourceManager _resourceManager;
        private readonly IYamlProcessor _yamlProcessor;
        private readonly string _resourcePath;
        private readonly Dictionary<string, ResourceTracker> _cachedResources;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Resource identifier
        /// </summary>
        public string ResourceId { get; set; }

        /// <summary>
        /// Resource type classification
        /// </summary>
        public string ResourceType { get; set; }

        /// <summary>
        /// Additional contextual metadata about the resource
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Performance and utilization metrics
        /// </summary>
        public Dictionary<string, double> Metrics { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Dependencies on other resources
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>
        /// When the resource was initially created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the resource was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Historical metrics for trend analysis
        /// </summary>
        public List<Dictionary<string, double>> MetricsHistory { get; set; } = new List<Dictionary<string, double>>();

        /// <summary>
        /// Status of the resource (Active, Degraded, Inactive)
        /// </summary>
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Health score from 0.0 to 1.0
        /// </summary>
        public double HealthScore { get; set; } = 1.0;

        /// <summary>
        /// Initializes a new instance of the ResourceTracker
        /// </summary>
        public ResourceTracker()
        {
            // Default constructor for serialization
        }

        /// <summary>
        /// Initializes a new instance of the ResourceTracker with all dependencies
        /// </summary>
        public ResourceTracker(
            ILogger<ResourceTracker> logger,
            IResourceManager resourceManager,
            IYamlProcessor yamlProcessor = null,
            string resourcePath = "resources")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _yamlProcessor = yamlProcessor;
            _resourcePath = resourcePath;
            _cachedResources = new Dictionary<string, ResourceTracker>();

            // Ensure resource directory exists
            Directory.CreateDirectory(_resourcePath);
        }

        /// <summary>
        /// Tracks a new resource within the system
        /// </summary>
        public async Task<ResourceTracker> TrackResourceAsync(Resource resource, FractalContext context)
        {
            // Delegate to the resource manager for actual tracking
            return await _resourceManager.TrackResourceAsync(resource, context);
        }

        /// <summary>
        /// Gets the current status of a tracked resource
        /// </summary>
        public async Task<ResourceTracker> GetResourceStatusAsync(string resourceId)
        {
            // First check the cache
            lock (_lockObject)
            {
                if (_cachedResources.TryGetValue(resourceId, out var cachedResource))
                {
                    return cachedResource;
                }
            }

            // Delegate to the resource manager if not in cache
            var resource = await _resourceManager.GetResourceStatusAsync(resourceId);

            // Update cache if found
            if (resource != null)
            {
                lock (_lockObject)
                {
                    _cachedResources[resourceId] = resource;
                }
            }

            return resource;
        }

        /// <summary>
        /// Updates resource metadata and tracking information
        /// </summary>
        public async Task<bool> UpdateResourceAsync(string resourceId, Dictionary<string, object> updates)
        {
            // Delegate to the resource manager
            var result = await _resourceManager.UpdateResourceAsync(resourceId, updates);

            // Update cache if successful
            if (result)
            {
                var updatedResource = await _resourceManager.GetResourceStatusAsync(resourceId);

                if (updatedResource != null)
                {
                    lock (_lockObject)
                    {
                        _cachedResources[resourceId] = updatedResource;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Validates a resource against specified criteria
        /// </summary>
        public async Task<bool> ValidateResourceAsync(string resourceId, Dictionary<string, object> criteria)
        {
            // Delegate to the resource manager
            return await _resourceManager.ValidateResourceAsync(resourceId, criteria);
        }

        /// <summary>
        /// Gets all resources matching specified criteria
        /// </summary>
        public async Task<IEnumerable<ResourceTracker>> GetResourcesAsync(Dictionary<string, object> filter = null)
        {
            var resources = new List<ResourceTracker>();
            var resourceFiles = Directory.GetFiles(_resourcePath, "*.yaml");

            foreach (var file in resourceFiles)
            {
                var resourceId = Path.GetFileNameWithoutExtension(file);
                var resource = await GetResourceStatusAsync(resourceId);

                if (resource != null && (filter == null || MatchesFilter(resource, filter)))
                {
                    resources.Add(resource);
                }
            }

            return resources;
        }

        /// <summary>
        /// Gets the health metrics for a resource
        /// </summary>
        public async Task<Dictionary<string, double>> GetResourceHealthMetricsAsync(string resourceId)
        {
            var resource = await GetResourceStatusAsync(resourceId);

            if (resource == null)
            {
                return new Dictionary<string, double>();
            }

            // Calculate health metrics from the resource data
            var healthMetrics = new Dictionary<string, double>
            {
                ["health_score"] = resource.HealthScore,
                ["uptime"] = CalculateUptime(resource),
                ["performance"] = resource.Metrics.GetValueOrDefault("performance", 0.0),
                ["error_rate"] = resource.Metrics.GetValueOrDefault("error_rate", 0.0),
                ["utilization"] = resource.Metrics.GetValueOrDefault("utilization", 0.0)
            };

            return healthMetrics;
        }

        /// <summary>
        /// Calculates the uptime for a resource based on its metrics history
        /// </summary>
        private double CalculateUptime(ResourceTracker resource)
        {
            // Simple implementation - could be enhanced with actual monitoring data
            return resource.Status == "Active" ? 1.0 : 0.0;
        }

        /// <summary>
        /// Checks if a resource matches the specified filter criteria
        /// </summary>
        private bool MatchesFilter(ResourceTracker resource, Dictionary<string, object> filter)
        {
            foreach (var (key, value) in filter)
            {
                // Check in metadata
                if (resource.Metadata.TryGetValue(key, out var metadataValue))
                {
                    if (!metadataValue.Equals(value))
                    {
                        return false;
                    }

                    continue;
                }

                // Check direct properties
                switch (key.ToLowerInvariant())
                {
                    case "resourceid":
                    case "resource_id":
                        if (!resource.ResourceId.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase))
                            return false;
                        break;

                    case "resourcetype":
                    case "resource_type":
                        if (!resource.ResourceType.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase))
                            return false;
                        break;

                    case "status":
                        if (!resource.Status.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase))
                            return false;
                        break;

                    default:
                        // If not found in metadata or as a direct property, consider it a non-match
                        return false;
                }
            }

            return true;
        }
    }

}