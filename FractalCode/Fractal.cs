using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace FractalCode.Core.Models
{
    /// <summary>
    /// Represents a fractal component in the system with its implementation, data, and knowledge layers.
    /// </summary>
    public class Fractal
    {
        /// <summary>
        /// Unique identifier for the fractal
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Name of the fractal
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Reference to the parent fractal, if any
        /// </summary>
        public string ParentName { get; set; }

        /// <summary>
        /// Template used for fractal creation
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// Current version of the fractal
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// When the fractal was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the fractal was last modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Implementation layer information
        /// </summary>
        public ImplementationLayer Implementation { get; set; }

        /// <summary>
        /// Data layer information
        /// </summary>
        public DataLayer Data { get; set; }

        /// <summary>
        /// Knowledge layer information
        /// </summary>
        public KnowledgeLayer Knowledge { get; set; }

        /// <summary>
        /// List of child fractals
        /// </summary>
        public List<string> Children { get; set; } = new List<string>();

        /// <summary>
        /// Dictionary of metadata attributes
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Validation state of the fractal
        /// </summary>
        public ValidationState ValidationState { get; set; }

        public Fractal()
        {
            Implementation = new ImplementationLayer();
            Data = new DataLayer();
            Knowledge = new KnowledgeLayer();
            ValidationState = new ValidationState();
        }
    }

    /// <summary>
    /// Represents the implementation layer of a fractal
    /// </summary>
    public class ImplementationLayer
    {
        /// <summary>
        /// Source code location
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// Test code location
        /// </summary>
        public string TestPath { get; set; }

        /// <summary>
        /// Build configuration
        /// </summary>
        public Dictionary<string, object> BuildConfig { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Implementation-specific dependencies
        /// </summary>
        public List<string> Dependencies { get; set; } = new List<string>();

        /// <summary>
        /// Implementation-level contracts
        /// </summary>
        public List<string> Contracts { get; set; } = new List<string>();

        /// <summary>
        /// Current implementation state
        /// </summary>
        public string State { get; set; }
    }

    /// <summary>
    /// Represents the data layer of a fractal
    /// </summary>
    public class DataLayer
    {
        /// <summary>
        /// Configuration data
        /// </summary>
        public Dictionary<string, object> Config { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Resource definitions
        /// </summary>
        public List<Resource> Resources { get; set; } = new List<Resource>();

        /// <summary>
        /// State management configuration
        /// </summary>
        public Dictionary<string, object> StateConfig { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Data persistence configuration
        /// </summary>
        public Dictionary<string, object> PersistenceConfig { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Current data state
        /// </summary>
        public string State { get; set; }
    }

    /// <summary>
    /// Represents the knowledge layer of a fractal
    /// </summary>
    public class KnowledgeLayer
    {
        /// <summary>
        /// Core contracts defining fractal behavior
        /// </summary>
        public List<Contract> Contracts { get; set; } = new List<Contract>();

        /// <summary>
        /// Architectural decisions and their context
        /// </summary>
        public List<Decision> Decisions { get; set; } = new List<Decision>();

        /// <summary>
        /// Evolution history of the fractal
        /// </summary>
        public List<EvolutionRecord> Evolution { get; set; } = new List<EvolutionRecord>();

        /// <summary>
        /// Dependencies and their context
        /// </summary>
        public List<DependencyContext> Dependencies { get; set; } = new List<DependencyContext>();

        /// <summary>
        /// Applied patterns and their context
        /// </summary>
        public List<PatternContext> Patterns { get; set; } = new List<PatternContext>();

        /// <summary>
        /// Knowledge base state
        /// </summary>
        public string State { get; set; }
    }

    /// <summary>
    /// Represents a resource in the fractal
    /// </summary>
    public class Resource
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public Dictionary<string, object> Specification { get; set; }
        public List<string> Dependencies { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// Represents a contract in the fractal
    /// </summary>
    public class Contract
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Version { get; set; }
        public Dictionary<string, object> Specification { get; set; }
        public List<string> Dependencies { get; set; }
        public ValidationState ValidationState { get; set; }
    }

    /// <summary>
    /// Represents an architectural decision
    /// </summary>
    public class Decision
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public string Title { get; set; }
        public string Context { get; set; }
        public string Rationale { get; set; }
        public List<string> Alternatives { get; set; }
        public List<string> Consequences { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// Represents an evolution record
    /// </summary>
    public class EvolutionRecord
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Changes { get; set; }
        public List<string> AffectedComponents { get; set; }
        public ValidationState ValidationState { get; set; }
    }

    /// <summary>
    /// Represents dependency context
    /// </summary>
    public class DependencyContext
    {
        public string DependencyId { get; set; }
        public string Type { get; set; }
        public string Version { get; set; }
        public string Context { get; set; }
        public List<string> Constraints { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// Represents pattern context
    /// </summary>
    public class PatternContext
    {
        public string PatternId { get; set; }
        public string Type { get; set; }
        public Dictionary<string, object> Implementation { get; set; }
        public List<string> ApplicabilityCriteria { get; set; }
        public Dictionary<string, object> ValidationResults { get; set; }
    }

    /// <summary>
    /// Represents validation state
    /// </summary>
    public class ValidationState
    {
        public bool IsValid { get; set; }
        public DateTime LastValidated { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public Dictionary<string, object> ValidationMetrics { get; set; } = new Dictionary<string, object>();
    }
}