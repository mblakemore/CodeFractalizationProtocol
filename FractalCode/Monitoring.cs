using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FractalCode.Monitoring
{
    /// <summary>
    /// Interface for collecting and tracking system metrics across fractal components.
    /// 
    /// Responsibility:
    /// - Tracking performance and health metrics for system components
    /// - Supporting observability across fractal boundaries
    /// - Providing data for health monitoring and adaptation decisions
    /// </summary>
    public interface IMetricsCollector
    {
        /// <summary>
        /// Records a numeric metric value with specified tags
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="value">Metric value</param>
        /// <param name="tags">Associated tags for metric categorization</param>
        Task RecordMetricAsync(string name, double value, Dictionary<string, string> tags = null);

        /// <summary>
        /// Increments a counter metric by the specified amount
        /// </summary>
        /// <param name="name">Counter name</param>
        /// <param name="increment">Amount to increment (default 1)</param>
        /// <param name="tags">Associated tags for metric categorization</param>
        Task IncrementCounterAsync(string name, int increment = 1, Dictionary<string, string> tags = null);

        /// <summary>
        /// Records the duration of an operation
        /// </summary>
        /// <param name="name">Timer name</param>
        /// <param name="milliseconds">Duration in milliseconds</param>
        /// <param name="tags">Associated tags for metric categorization</param>
        Task RecordTimingAsync(string name, long milliseconds, Dictionary<string, string> tags = null);

        /// <summary>
        /// Records resource utilization metrics
        /// </summary>
        /// <param name="resourceType">Type of resource (memory, cpu, etc.)</param>
        /// <param name="utilization">Utilization value (typically 0-100)</param>
        /// <param name="metadata">Additional metadata about the resource</param>
        Task RecordResourceUtilizationAsync(string resourceType, double utilization, Dictionary<string, object> metadata = null);

        /// <summary>
        /// Gets metrics for a specific component
        /// </summary>
        /// <param name="componentName">Name of the component</param>
        /// <returns>Dictionary of metrics for the component</returns>
        Task<Dictionary<string, double>> GetComponentMetricsAsync(string componentName);

        /// <summary>
        /// Creates a new metrics context for a specific operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="componentName">Component initiating the operation</param>
        /// <returns>A metrics context that can be used to track the operation</returns>
        IMetricsContext CreateContext(string operationName, string componentName);
    }

    /// <summary>
    /// Context for tracking metrics within a specific operation boundary
    /// </summary>
    public interface IMetricsContext : IDisposable
    {
        /// <summary>
        /// Records a metric within this context
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="value">Metric value</param>
        void RecordMetric(string name, double value);

        /// <summary>
        /// Adds a tag to the context
        /// </summary>
        /// <param name="key">Tag key</param>
        /// <param name="value">Tag value</param>
        void AddTag(string key, string value);

        /// <summary>
        /// Marks the operation as successful or failed
        /// </summary>
        /// <param name="success">Whether the operation succeeded</param>
        /// <param name="errorInfo">Additional error information if failed</param>
        void SetResult(bool success, string errorInfo = null);
    }

    /// <summary>
    /// Implementation of the metrics collector with support for the Code Fractalization Protocol.
    /// 
    /// Design decisions:
    /// - Uses non-blocking concurrent collections for thread safety
    /// - Implements flexible tagging for metric categorization
    /// - Supports hierarchical component metrics aligned with fractal structure
    /// - Provides context tracking for operation-specific metrics
    /// </summary>
    public class MetricsCollector : IMetricsCollector
    {
        private readonly ILogger<MetricsCollector> _logger;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, double>> _componentMetrics;
        private readonly ConcurrentDictionary<string, ConcurrentBag<MetricEntry>> _metricHistory;

        /// <summary>
        /// Initializes a new instance of the MetricsCollector
        /// </summary>
        /// <param name="logger">Logger for tracking metrics collection events</param>
        public MetricsCollector(ILogger<MetricsCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _componentMetrics = new ConcurrentDictionary<string, ConcurrentDictionary<string, double>>();
            _metricHistory = new ConcurrentDictionary<string, ConcurrentBag<MetricEntry>>();
        }

        /// <inheritdoc/>
        public async Task RecordMetricAsync(string name, double value, Dictionary<string, string> tags = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Metric name cannot be empty", nameof(name));

            try
            {
                // Determine component from tags
                var component = GetComponentFromTags(tags);

                // Update component metrics
                var metrics = _componentMetrics.GetOrAdd(component, _ => new ConcurrentDictionary<string, double>());
                metrics[name] = value;

                // Record in history
                var history = _metricHistory.GetOrAdd(name, _ => new ConcurrentBag<MetricEntry>());
                history.Add(new MetricEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Value = value,
                    Tags = tags ?? new Dictionary<string, string>()
                });

                _logger.LogDebug("Recorded metric {Name} = {Value} for component {Component}",
                    name, value, component);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording metric {Name}", name);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task IncrementCounterAsync(string name, int increment = 1, Dictionary<string, string> tags = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Counter name cannot be empty", nameof(name));

            try
            {
                // Determine component from tags
                var component = GetComponentFromTags(tags);

                // Update component metrics
                var metrics = _componentMetrics.GetOrAdd(component, _ => new ConcurrentDictionary<string, double>());
                metrics.AddOrUpdate(name, increment, (_, current) => current + increment);

                // Record in history
                var history = _metricHistory.GetOrAdd(name, _ => new ConcurrentBag<MetricEntry>());
                history.Add(new MetricEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Value = increment,
                    Tags = tags ?? new Dictionary<string, string>(),
                    IsIncrement = true
                });

                _logger.LogDebug("Incremented counter {Name} by {Increment} for component {Component}",
                    name, increment, component);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing counter {Name}", name);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task RecordTimingAsync(string name, long milliseconds, Dictionary<string, string> tags = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Timer name cannot be empty", nameof(name));

            if (milliseconds < 0)
                throw new ArgumentException("Duration cannot be negative", nameof(milliseconds));

            try
            {
                // Add timing tag
                tags = tags ?? new Dictionary<string, string>();
                tags["metric_type"] = "timing";

                // Record as regular metric
                await RecordMetricAsync(name, milliseconds, tags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording timing {Name}", name);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task RecordResourceUtilizationAsync(string resourceType, double utilization, Dictionary<string, object> metadata = null)
        {
            if (string.IsNullOrEmpty(resourceType))
                throw new ArgumentException("Resource type cannot be empty", nameof(resourceType));

            try
            {
                // Convert metadata to tags
                var tags = new Dictionary<string, string>
                {
                    ["resource_type"] = resourceType,
                    ["metric_type"] = "utilization"
                };

                if (metadata != null)
                {
                    foreach (var (key, value) in metadata)
                    {
                        if (value != null)
                            tags[$"meta_{key}"] = value.ToString();
                    }
                }

                // Record as regular metric
                await RecordMetricAsync($"resource.{resourceType}.utilization", utilization, tags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording resource utilization for {ResourceType}", resourceType);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, double>> GetComponentMetricsAsync(string componentName)
        {
            if (string.IsNullOrEmpty(componentName))
                throw new ArgumentException("Component name cannot be empty", nameof(componentName));

            try
            {
                if (_componentMetrics.TryGetValue(componentName, out var metrics))
                {
                    return new Dictionary<string, double>(metrics);
                }

                return new Dictionary<string, double>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics for component {Component}", componentName);
                throw;
            }
        }

        /// <inheritdoc/>
        public IMetricsContext CreateContext(string operationName, string componentName)
        {
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentException("Operation name cannot be empty", nameof(operationName));

            if (string.IsNullOrEmpty(componentName))
                throw new ArgumentException("Component name cannot be empty", nameof(componentName));

            return new MetricsContext(this, operationName, componentName);
        }

        private string GetComponentFromTags(Dictionary<string, string> tags)
        {
            if (tags != null && tags.TryGetValue("component", out var component))
                return component;

            return "global";
        }

        /// <summary>
        /// Internal class for tracking individual metric entries
        /// </summary>
        private class MetricEntry
        {
            public DateTime Timestamp { get; set; }
            public double Value { get; set; }
            public Dictionary<string, string> Tags { get; set; }
            public bool IsIncrement { get; set; }
        }

        /// <summary>
        /// Implementation of metrics context for operation tracking
        /// </summary>
        private class MetricsContext : IMetricsContext
        {
            private readonly IMetricsCollector _collector;
            private readonly string _operationName;
            private readonly string _componentName;
            private readonly Dictionary<string, string> _tags;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            public MetricsContext(IMetricsCollector collector, string operationName, string componentName)
            {
                _collector = collector;
                _operationName = operationName;
                _componentName = componentName;
                _tags = new Dictionary<string, string>
                {
                    ["component"] = componentName,
                    ["operation"] = operationName
                };
                _stopwatch = Stopwatch.StartNew();
            }

            public void RecordMetric(string name, double value)
            {
                _collector.RecordMetricAsync($"{_operationName}.{name}", value, _tags).GetAwaiter().GetResult();
            }

            public void AddTag(string key, string value)
            {
                _tags[key] = value;
            }

            public void SetResult(bool success, string errorInfo = null)
            {
                _tags["success"] = success.ToString().ToLower();

                if (!success && !string.IsNullOrEmpty(errorInfo))
                {
                    _tags["error"] = errorInfo;
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _stopwatch.Stop();
                _collector.RecordTimingAsync($"{_operationName}.duration", _stopwatch.ElapsedMilliseconds, _tags)
                    .GetAwaiter().GetResult();

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Interface for telemetry collection and distribution across fractal boundaries.
    /// 
    /// Responsibility:
    /// - Collecting distributed trace information
    /// - Correlating events across fractal components
    /// - Supporting distributed debugging and observability
    /// - Providing a unified view of system behavior
    /// </summary>
    public interface ITelemetryProvider
    {
        /// <summary>
        /// Starts a new telemetry operation
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="metadata">Additional metadata about the operation</param>
        /// <returns>A telemetry context to track the operation</returns>
        ITelemetryContext StartOperation(string operationName, Dictionary<string, object> metadata = null);

        /// <summary>
        /// Logs an event with the telemetry system
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="properties">Properties associated with the event</param>
        Task LogEventAsync(string eventName, Dictionary<string, object> properties = null);

        /// <summary>
        /// Tracks a dependency call to an external system
        /// </summary>
        /// <param name="dependencyType">Type of dependency (SQL, HTTP, etc.)</param>
        /// <param name="targetName">Name of the target system</param>
        /// <param name="operationName">Name of the operation being performed</param>
        /// <param name="durationMs">Duration of the call in milliseconds</param>
        /// <param name="success">Whether the call succeeded</param>
        /// <param name="properties">Additional properties about the call</param>
        Task TrackDependencyAsync(string dependencyType, string targetName, string operationName,
            long durationMs, bool success, Dictionary<string, object> properties = null);

        /// <summary>
        /// Tracks an exception with the telemetry system
        /// </summary>
        /// <param name="exception">The exception to track</param>
        /// <param name="properties">Additional properties about the exception</param>
        Task TrackExceptionAsync(Exception exception, Dictionary<string, object> properties = null);

        /// <summary>
        /// Gets active telemetry context or creates a new one
        /// </summary>
        /// <returns>Current telemetry context</returns>
        ITelemetryContext GetCurrentContext();

        /// <summary>
        /// Sets the global property for all telemetry events
        /// </summary>
        /// <param name="key">Property name</param>
        /// <param name="value">Property value</param>
        void SetGlobalProperty(string key, object value);
    }

    /// <summary>
    /// Context for tracking a telemetry operation
    /// </summary>
    public interface ITelemetryContext : IDisposable
    {
        /// <summary>
        /// Gets the operation ID for this context
        /// </summary>
        string OperationId { get; }

        /// <summary>
        /// Gets the parent operation ID, if any
        /// </summary>
        string ParentOperationId { get; }

        /// <summary>
        /// Gets or sets the operation name
        /// </summary>
        string OperationName { get; set; }

        /// <summary>
        /// Adds a property to the telemetry context
        /// </summary>
        /// <param name="key">Property name</param>
        /// <param name="value">Property value</param>
        void SetProperty(string key, object value);

        /// <summary>
        /// Marks the operation as successful or failed
        /// </summary>
        /// <param name="success">Whether the operation succeeded</param>
        /// <param name="resultDescription">Description of the result</param>
        void SetResult(bool success, string resultDescription = null);

        /// <summary>
        /// Creates a child operation context
        /// </summary>
        /// <param name="operationName">Name of the child operation</param>
        /// <returns>A new telemetry context for the child operation</returns>
        ITelemetryContext CreateChildContext(string operationName);
    }


    /// <summary>
    /// Implementation of the telemetry provider with support for the Code Fractalization Protocol.
    /// 
    /// Design decisions:
    /// - Implements distributed tracing with operation ID correlation
    /// - Supports hierarchical operation tracking aligned with fractal structure
    /// - Provides context propagation across component boundaries
    /// - Includes global property support for system-wide context
    /// </summary>
    public class TelemetryProvider : ITelemetryProvider
    {
        private readonly ILogger<TelemetryProvider> _logger;
        private readonly ConcurrentDictionary<string, object> _globalProperties;
        private readonly AsyncLocal<ITelemetryContext> _currentContext;

        /// <summary>
        /// Initializes a new instance of the TelemetryProvider
        /// </summary>
        /// <param name="logger">Logger for tracking telemetry events</param>
        public TelemetryProvider(ILogger<TelemetryProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _globalProperties = new ConcurrentDictionary<string, object>();
            _currentContext = new AsyncLocal<ITelemetryContext>();
        }

        /// <inheritdoc/>
        public ITelemetryContext StartOperation(string operationName, Dictionary<string, object> metadata = null)
        {
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentException("Operation name cannot be empty", nameof(operationName));

            // Create new context
            var context = new TelemetryContext(this, operationName, null, Guid.NewGuid().ToString(), metadata);

            // Set as current context
            _currentContext.Value = context;

            _logger.LogDebug("Started operation {OperationName} with ID {OperationId}",
                operationName, context.OperationId);

            return context;
        }

        /// <inheritdoc/>
        public async Task LogEventAsync(string eventName, Dictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException("Event name cannot be empty", nameof(eventName));

            try
            {
                // Combine properties
                properties = CombineProperties(properties);

                // Add current operation context if available
                var context = GetCurrentContext();
                if (context != null)
                {
                    properties["operationId"] = context.OperationId;
                    properties["operationName"] = context.OperationName;

                    if (!string.IsNullOrEmpty(context.ParentOperationId))
                    {
                        properties["parentOperationId"] = context.ParentOperationId;
                    }
                }

                // Log the event
                _logger.LogInformation("Telemetry event: {EventName} {@Properties}", eventName, properties);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging telemetry event {EventName}", eventName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task TrackDependencyAsync(string dependencyType, string targetName, string operationName,
            long durationMs, bool success, Dictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(dependencyType))
                throw new ArgumentException("Dependency type cannot be empty", nameof(dependencyType));

            if (string.IsNullOrEmpty(targetName))
                throw new ArgumentException("Target name cannot be empty", nameof(targetName));

            try
            {
                // Combine properties
                properties = CombineProperties(properties);

                // Add dependency information
                properties["dependencyType"] = dependencyType;
                properties["target"] = targetName;
                properties["duration"] = durationMs;
                properties["success"] = success;

                // Add current operation context if available
                var context = GetCurrentContext();
                if (context != null)
                {
                    properties["operationId"] = context.OperationId;
                    properties["operationName"] = context.OperationName;
                }

                // Log the dependency
                _logger.LogInformation("Dependency call: {Target}.{Operation} ({Type}) {Status} {Duration}ms {@Properties}",
                    targetName, operationName, dependencyType, success ? "succeeded" : "failed", durationMs, properties);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking dependency {Type} to {Target}", dependencyType, targetName);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task TrackExceptionAsync(Exception exception, Dictionary<string, object> properties = null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            try
            {
                // Combine properties
                properties = CombineProperties(properties);

                // Add exception information
                properties["exceptionType"] = exception.GetType().Name;
                properties["exceptionMessage"] = exception.Message;

                // Add current operation context if available
                var context = GetCurrentContext();
                if (context != null)
                {
                    properties["operationId"] = context.OperationId;
                    properties["operationName"] = context.OperationName;

                    // Mark operation as failed
                    context.SetResult(false, exception.Message);
                }

                // Log the exception
                _logger.LogError(exception, "Tracked exception: {ExceptionType} {@Properties}",
                    exception.GetType().Name, properties);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking exception {ExceptionType}", exception.GetType().Name);
                throw;
            }
        }

        /// <inheritdoc/>
        public ITelemetryContext GetCurrentContext()
        {
            return _currentContext.Value;
        }

        /// <inheritdoc/>
        public void SetGlobalProperty(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Property key cannot be empty", nameof(key));

            _globalProperties[key] = value;
        }

        private Dictionary<string, object> CombineProperties(Dictionary<string, object> properties)
        {
            var result = new Dictionary<string, object>(_globalProperties);

            if (properties != null)
            {
                foreach (var (key, value) in properties)
                {
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Internal implementation of telemetry context
        /// </summary>
        private class TelemetryContext : ITelemetryContext
        {
            private readonly TelemetryProvider _provider;
            private readonly Dictionary<string, object> _properties;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;

            public string OperationId { get; }

            public string ParentOperationId { get; }

            public string OperationName { get; set; }

            public TelemetryContext(TelemetryProvider provider, string operationName,
                string parentOperationId, string operationId, Dictionary<string, object> metadata)
            {
                _provider = provider;
                OperationName = operationName;
                ParentOperationId = parentOperationId;
                OperationId = operationId;

                _properties = new Dictionary<string, object>();

                if (metadata != null)
                {
                    foreach (var (key, value) in metadata)
                    {
                        _properties[key] = value;
                    }
                }

                _stopwatch = Stopwatch.StartNew();

                // Log operation start
                _provider._logger.LogDebug("Operation started: {OperationName} (ID: {OperationId}, Parent: {ParentId})",
                    operationName, operationId, parentOperationId ?? "none");
            }

            public void SetProperty(string key, object value)
            {
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentException("Property key cannot be empty", nameof(key));

                _properties[key] = value;
            }

            public void SetResult(bool success, string resultDescription = null)
            {
                _properties["success"] = success;

                if (!string.IsNullOrEmpty(resultDescription))
                {
                    _properties["resultDescription"] = resultDescription;
                }
            }

            public ITelemetryContext CreateChildContext(string operationName)
            {
                if (string.IsNullOrEmpty(operationName))
                    throw new ArgumentException("Operation name cannot be empty", nameof(operationName));

                var childContext = new TelemetryContext(
                    _provider,
                    operationName,
                    OperationId,
                    Guid.NewGuid().ToString(),
                    null);

                // Restore original context when child is disposed
                var originalContext = _provider._currentContext.Value;

                // Set child as current
                _provider._currentContext.Value = childContext;

                // Return wrapped context that will restore original on disposal
                return new ChildTelemetryContext(childContext, originalContext, _provider);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _stopwatch.Stop();
                var durationMs = _stopwatch.ElapsedMilliseconds;

                // Set duration
                _properties["duration"] = durationMs;

                // Log operation completion
                var success = _properties.TryGetValue("success", out var successObj) &&
                              successObj is bool successValue && successValue;

                _provider._logger.LogDebug(
                    "Operation completed: {OperationName} (ID: {OperationId}) {Status} in {Duration}ms {@Properties}",
                    OperationName, OperationId, success ? "succeeded" : "failed", durationMs, _properties);

                _disposed = true;
            }
        }

        /// <summary>
        /// Special context wrapper for child operations that restores parent context on disposal
        /// </summary>
        private class ChildTelemetryContext : ITelemetryContext
        {
            private readonly ITelemetryContext _innerContext;
            private readonly ITelemetryContext _parentContext;
            private readonly TelemetryProvider _provider;

            public string OperationId => _innerContext.OperationId;

            public string ParentOperationId => _innerContext.ParentOperationId;

            public string OperationName
            {
                get => _innerContext.OperationName;
                set => _innerContext.OperationName = value;
            }

            public ChildTelemetryContext(ITelemetryContext innerContext, ITelemetryContext parentContext,
                TelemetryProvider provider)
            {
                _innerContext = innerContext;
                _parentContext = parentContext;
                _provider = provider;
            }

            public void SetProperty(string key, object value)
            {
                _innerContext.SetProperty(key, value);
            }

            public void SetResult(bool success, string resultDescription = null)
            {
                _innerContext.SetResult(success, resultDescription);
            }

            public ITelemetryContext CreateChildContext(string operationName)
            {
                return _innerContext.CreateChildContext(operationName);
            }

            public void Dispose()
            {
                // Dispose inner context
                _innerContext.Dispose();

                // Restore parent context
                _provider._currentContext.Value = _parentContext;
            }
        }
    }

}