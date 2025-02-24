using System;
using System.Collections.Generic;

namespace FractalCode.Patterns
{
    /// <summary>
    /// Represents the results of pattern validation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Indicates overall validation success
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of critical validation failures
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Non-critical validation observations
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// When the validation occurred
        /// </summary>
        public DateTime ValidationDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Quantitative validation metrics
        /// </summary>
        public Dictionary<string, double> ValidationMetrics { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Rules that were checked during validation
        /// </summary>
        public List<string> CheckedRules { get; set; } = new List<string>();

        /// <summary>
        /// Success rate of previous applications (if available)
        /// </summary>
        public double HistoricalSuccessRate { get; set; }
    }
}