using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace FractalCode.Logging
{
    /// <summary>
    /// Provides structured logging capabilities aligned with the Code Fractalization Protocol.
    /// 
    /// This provider enhances standard logging with:
    /// - Structured log entries with consistent metadata
    /// - Correlation IDs for tracking operations across fractals
    /// - Context preservation for decision tracking
    /// - Fractal hierarchy awareness
    /// 
    /// Implementation decisions:
    /// - Uses thread-local storage for correlation context
    /// - Maintains a concurrent dictionary of loggers for thread safety
    /// - Preserves fractal hierarchy in log structure
    /// </summary>
    public class StructuredLoggingProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, StructuredLogger> _loggers = new();
        private readonly LogLevel _minLevel;
        private readonly StructuredLoggingOptions _options;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="StructuredLoggingProvider"/> class.
        /// </summary>
        /// <param name="options">Configuration options for structured logging</param>
        public StructuredLoggingProvider(StructuredLoggingOptions options = null)
        {
            _options = options ?? new StructuredLoggingOptions();
            _minLevel = _options.MinimumLevel;
        }

        /// <summary>
        /// Creates a logger with the given category name.
        /// </summary>
        /// <param name="categoryName">The category name for the logger</param>
        /// <returns>An instance of <see cref="ILogger"/></returns>
        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new StructuredLogger(name, _minLevel, _options));
        }

        /// <summary>
        /// Disposes the provider and releases any resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the provider resources.
        /// </summary>
        /// <param name="disposing">Whether the method is called from Dispose()</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _loggers.Clear();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Options for configuring the structured logging behavior.
    /// </summary>
    public class StructuredLoggingOptions
    {
        /// <summary>
        /// Gets or sets the minimum log level.
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets whether to include scopes in the log output.
        /// </summary>
        public bool IncludeScopes { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to include fractal context in the log output.
        /// </summary>
        public bool IncludeFractalContext { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to track decision context in logs.
        /// </summary>
        public bool TrackDecisionContext { get; set; } = true;

        /// <summary>
        /// Gets or sets the correlation ID field name.
        /// </summary>
        public string CorrelationIdField { get; set; } = "CorrelationId";

        /// <summary>
        /// Gets or sets the fractal context field name.
        /// </summary>
        public string FractalContextField { get; set; } = "FractalContext";

        /// <summary>
        /// Gets or sets custom metadata to include with all logs.
        /// </summary>
        public Dictionary<string, object> DefaultMetadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Implementation of the structured logger that maintains context and correlation.
    /// </summary>
    internal class StructuredLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogLevel _minLevel;
        private readonly StructuredLoggingOptions _options;
        private static readonly AsyncLocal<string> _correlationId = new AsyncLocal<string>();
        private static readonly AsyncLocal<Dictionary<string, object>> _fractalContext = new AsyncLocal<Dictionary<string, object>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="StructuredLogger"/> class.
        /// </summary>
        /// <param name="categoryName">Category name for the logger</param>
        /// <param name="minLevel">Minimum log level</param>
        /// <param name="options">Structured logging options</param>
        public StructuredLogger(string categoryName, LogLevel minLevel, StructuredLoggingOptions options)
        {
            _categoryName = categoryName;
            _minLevel = minLevel;
            _options = options;
        }

        /// <summary>
        /// Begins a logging scope.
        /// </summary>
        /// <typeparam name="TState">The type of the state</typeparam>
        /// <param name="state">The state to associate with the scope</param>
        /// <returns>A disposable that ends the scope when disposed</returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            if (state is Dictionary<string, object> properties)
            {
                return new FractalScope(properties);
            }

            return new FractalScope(new Dictionary<string, object> { { "Scope", state } });
        }

        /// <summary>
        /// Checks if logging is enabled for the specified level.
        /// </summary>
        /// <param name="logLevel">The log level to check</param>
        /// <returns>True if logging is enabled, false otherwise</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <typeparam name="TState">The type of the state</typeparam>
        /// <param name="logLevel">The log level</param>
        /// <param name="eventId">The event ID</param>
        /// <param name="state">The state</param>
        /// <param name="exception">The exception</param>
        /// <param name="formatter">The formatter function</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            // Ensure correlation ID exists
            var correlationId = _correlationId.Value;
            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
                _correlationId.Value = correlationId;
            }

            // Build the structured log entry
            var logEntry = new Dictionary<string, object>
            {
                { "Timestamp", DateTime.UtcNow },
                { "Level", logLevel.ToString() },
                { "Category", _categoryName },
                { "Message", message },
                { _options.CorrelationIdField, correlationId },
                { "EventId", eventId.Id }
            };

            // Add the fractal context if available and enabled
            if (_options.IncludeFractalContext && _fractalContext.Value != null)
            {
                logEntry[_options.FractalContextField] = _fractalContext.Value;
            }

            // Add custom metadata
            if (_options.DefaultMetadata != null)
            {
                foreach (var item in _options.DefaultMetadata)
                {
                    logEntry[item.Key] = item.Value;
                }
            }

            // Add exception details if present
            if (exception != null)
            {
                logEntry["Exception"] = new
                {
                    exception.Message,
                    exception.StackTrace,
                    Type = exception.GetType().FullName,
                    InnerException = exception.InnerException?.Message
                };
            }

            // In a real implementation, you would write to your structured logging sink here
            // For this example, we'll just convert to JSON and write to console
            var json = System.Text.Json.JsonSerializer.Serialize(logEntry, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Choose output color based on log level
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = GetLogLevelColor(logLevel);

            Console.WriteLine(json);

            // Restore original color
            Console.ForegroundColor = originalColor;
        }

        /// <summary>
        /// Gets a console color for the specified log level.
        /// </summary>
        /// <param name="logLevel">The log level</param>
        /// <returns>A console color</returns>
        private static ConsoleColor GetLogLevelColor(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Critical => ConsoleColor.Red,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Trace => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };
        }

        /// <summary>
        /// Scope implementation for preserving fractal context.
        /// </summary>
        private class FractalScope : IDisposable
        {
            private readonly Dictionary<string, object> _previousContext;
            private readonly Dictionary<string, object> _scopeProperties;
            private bool _disposed = false;

            public FractalScope(Dictionary<string, object> properties)
            {
                _scopeProperties = properties;
                _previousContext = _fractalContext.Value;

                // Create or update the context
                if (_fractalContext.Value == null)
                {
                    _fractalContext.Value = new Dictionary<string, object>(_scopeProperties);
                }
                else
                {
                    // Merge properties into existing context
                    var newContext = new Dictionary<string, object>(_fractalContext.Value);
                    foreach (var prop in _scopeProperties)
                    {
                        newContext[prop.Key] = prop.Value;
                    }
                    _fractalContext.Value = newContext;
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _fractalContext.Value = _previousContext;
                    _disposed = true;
                }
            }
        }
    }
}