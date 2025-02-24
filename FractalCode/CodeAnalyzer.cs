using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using FractalCode.Core.Models;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FractalCode.Analysis
{
    // AnalysisResults, ComponentInfo, and DependencyInfo classes remain unchanged...

    public class CodeAnalyzer : ICodeAnalyzer
    {
        private readonly ILogger<CodeAnalyzer> _logger;
        private const double COMPLEXITY_THRESHOLD = 0.65;

        public CodeAnalyzer(ILogger<CodeAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AnalysisResults> AnalyzeCodebaseAsync(string path)
        {
            try
            {
                _logger.LogInformation($"Starting codebase analysis at: {path}");

                var results = new AnalysisResults();

                // Analyze each .cs file
                foreach (var file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                {
                    await AnalyzeSourceFileAsync(file, results);
                }

                // Generate decomposition advice
                GenerateDecompositionAdvice(results);

                _logger.LogInformation("Analysis completed successfully");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during codebase analysis");
                throw;
            }
        }

        public async Task SaveAnalysisResultsAsync(AnalysisResults results, string outputPath)
        {
            try
            {
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var yaml = serializer.Serialize(results);
                await File.WriteAllTextAsync(outputPath, yaml);

                _logger.LogInformation($"Analysis results saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving analysis results");
                throw;
            }
        }

        private async Task AnalyzeSourceFileAsync(string filePath, AnalysisResults results)
        {
            try
            {
                var sourceText = await File.ReadAllTextAsync(filePath);
                var tree = CSharpSyntaxTree.ParseText(sourceText);
                var root = await tree.GetRootAsync();
                var compilation = CSharpCompilation.Create("Analysis")
                    .AddSyntaxTrees(tree);
                var semanticModel = compilation.GetSemanticModel(tree);

                // Find all classes
                var classDeclarations = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>();

                foreach (var classDeclaration in classDeclarations)
                {
                    var classAnalyzer = new ClassAnalyzer(classDeclaration, semanticModel);
                    var componentInfo = await CreateComponentInfoAsync(classAnalyzer, filePath);
                    results.Components.Add(componentInfo);

                    // Add dependencies
                    var dependencies = classAnalyzer.AnalyzeDependencies();
                    foreach (var dep in componentInfo.Dependencies)
                    {
                        results.Dependencies.Add(new DependencyInfo
                        {
                            Source = componentInfo.Name,
                            Target = dep,
                            Type = "class",
                            Strength = CalculateDependencyStrength(dependencies, dep)
                        });
                    }

                    // Add complexity score
                    results.CognitiveComplexity[componentInfo.Name] = classAnalyzer.CognitiveComplexity;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing file: {filePath}");
                throw;
            }
        }

        private async Task<ComponentInfo> CreateComponentInfoAsync(ClassAnalyzer analyzer, string filePath)
        {
            var componentInfo = new ComponentInfo
            {
                Name = analyzer.Identifier.Text,
                Path = filePath,
                CognitiveComplexity = analyzer.CognitiveComplexity
            };

            // Get dependencies from analyzer
            var dependencies = analyzer.AnalyzeDependencies();
            componentInfo.Dependencies = ExtractDependencies(dependencies);

            // Get responsibilities from analyzer
            componentInfo.Responsibilities = analyzer.AnalyzeResponsibilities();

            return componentInfo;
        }

        private List<string> ExtractDependencies(Dictionary<string, object> dependencies)
        {
            var result = new HashSet<string>();

            if (dependencies.ContainsKey("inheritance"))
            {
                var inheritanceDeps = dependencies["inheritance"] as List<string>;
                if (inheritanceDeps != null)
                {
                    result.UnionWith(inheritanceDeps);
                }
            }

            if (dependencies.ContainsKey("fields"))
            {
                var fields = dependencies["fields"] as List<object>;
                if (fields != null)
                {
                    foreach (dynamic field in fields)
                    {
                        try
                        {
                            string typeStr = field.Type?.ToString();
                            if (!string.IsNullOrEmpty(typeStr))
                            {
                                result.Add(typeStr);
                            }
                        }
                        catch
                        {
                            // Skip if dynamic access fails
                            continue;
                        }
                    }
                }
            }

            if (dependencies.ContainsKey("methods"))
            {
                var methods = dependencies["methods"] as List<object>;
                if (methods != null)
                {
                    foreach (dynamic method in methods)
                    {
                        try
                        {
                            string returnType = method.ReturnType?.ToString();
                            if (!string.IsNullOrEmpty(returnType))
                            {
                                result.Add(returnType);
                            }

                            var parameters = method.Parameters as IEnumerable<string>;
                            if (parameters != null)
                            {
                                result.UnionWith(parameters);
                            }
                        }
                        catch
                        {
                            // Skip if dynamic access fails
                            continue;
                        }
                    }
                }
            }

            return result.ToList();
        }

        private double CalculateDependencyStrength(Dictionary<string, object> dependencies, string target)
        {
            int references = 0;

            // Count references in fields
            if (dependencies.ContainsKey("fields"))
            {
                references += ((List<object>)dependencies["fields"])
                    .Count(f => ((dynamic)f).Type.ToString() == target);
            }

            // Count references in methods
            if (dependencies.ContainsKey("methods"))
            {
                var methods = (List<object>)dependencies["methods"];
                foreach (var method in methods)
                {
                    if (((dynamic)method).ReturnType.ToString() == target)
                        references++;
                    references += ((List<string>)((dynamic)method).Parameters)
                        .Count(p => p == target);
                }
            }

            // Normalize to 0-1 range
            return Math.Min(references / 10.0, 1.0);
        }

        private void GenerateDecompositionAdvice(AnalysisResults results)
        {
            foreach (var component in results.Components)
            {
                // Check cognitive complexity
                if (component.CognitiveComplexity > COMPLEXITY_THRESHOLD)
                {
                    var advice = new List<string>
                    {
                        $"Component {component.Name} exceeds complexity threshold:",
                        $"- Current complexity: {component.CognitiveComplexity:F2}",
                        $"- Threshold: {COMPLEXITY_THRESHOLD}",
                        "Consider splitting into multiple components based on responsibilities:"
                    };

                    advice.AddRange(SuggestDecomposition(component));
                    results.DecompositionAdvice.AddRange(advice);
                }

                // Check dependency count
                if (component.Dependencies.Count > 5)
                {
                    results.DecompositionAdvice.Add(
                        $"Component {component.Name} has high dependency count ({component.Dependencies.Count}). " +
                        "Consider reducing coupling through interface abstraction or responsibility redistribution."
                    );
                }
            }

            // Check for circular dependencies
            var circularDeps = FindCircularDependencies(results.Dependencies);
            if (circularDeps.Any())
            {
                results.DecompositionAdvice.Add(
                    "Detected circular dependencies: " +
                    string.Join(", ", circularDeps.Select(d => $"{d.Source} -> {d.Target}"))
                );
            }
        }

        private IEnumerable<string> SuggestDecomposition(ComponentInfo component)
        {
            var suggestions = new List<string>();

            // Group responsibilities
            var groups = component.Responsibilities
                .GroupBy(r => r.Split(' ')[0]) // Group by first word (e.g., "Handles", "Provides")
                .Where(g => g.Count() >= 3);

            foreach (var group in groups)
            {
                suggestions.Add(
                    $"- Consider extracting {group.Key} responsibilities into separate component: " +
                    string.Join(", ", group.Select(r => r.Split(' ')[1]))
                );
            }

            return suggestions;
        }

        private IEnumerable<DependencyInfo> FindCircularDependencies(List<DependencyInfo> dependencies)
        {
            var circularDeps = new List<DependencyInfo>();

            foreach (var dep in dependencies)
            {
                var reverse = dependencies.FirstOrDefault(
                    d => d.Source == dep.Target && d.Target == dep.Source
                );

                if (reverse != null)
                {
                    circularDeps.Add(dep);
                }
            }

            return circularDeps;
        }
    }
  
    /// <summary>
    /// Interface for analyzing codebases using Code Fractalization principles.
    /// Provides methods for analyzing code structure, complexity, and relationships.
    /// </summary>
    public interface ICodeAnalyzer
    {
        /// <summary>
        /// Analyzes a codebase at the specified path, processing all source files
        /// to generate comprehensive analysis results.
        /// </summary>
        /// <param name="path">The root path of the codebase to analyze</param>
        /// <returns>
        /// Analysis results containing component information, dependencies,
        /// complexity metrics, and suggested decomposition advice
        /// </returns>
        /// <exception cref="DirectoryNotFoundException">If the specified path does not exist</exception>
        /// <exception cref="InvalidOperationException">If the codebase cannot be analyzed</exception>
        Task<AnalysisResults> AnalyzeCodebaseAsync(string path);

        /// <summary>
        /// Saves analysis results to a file in YAML format.
        /// </summary>
        /// <param name="results">The analysis results to save</param>
        /// <param name="outputPath">Path where the results should be saved</param>
        /// <exception cref="ArgumentNullException">If results is null</exception>
        /// <exception cref="IOException">If the file cannot be written</exception>
        Task SaveAnalysisResultsAsync(AnalysisResults results, string outputPath);
    }
    /// <summary>
    /// Contains the complete results of a codebase analysis, including component information,
    /// dependencies, complexity metrics, and suggested improvements.
    /// </summary>
    public class AnalysisResults
    {
        /// <summary>
        /// Information about each component (class, module, etc.) found in the codebase
        /// </summary>
        public List<ComponentInfo> Components { get; set; } = new();

        /// <summary>
        /// Relationships and dependencies between components
        /// </summary>
        public List<DependencyInfo> Dependencies { get; set; } = new();

        /// <summary>
        /// Cognitive complexity scores for each component
        /// </summary>
        public Dictionary<string, double> CognitiveComplexity { get; set; } = new();

        /// <summary>
        /// Suggestions for improving code organization and structure
        /// </summary>
        public List<string> DecompositionAdvice { get; set; } = new();

        /// <summary>
        /// When the analysis was performed
        /// </summary>
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Summary statistics about the analyzed codebase
        /// </summary>
        public CodebaseMetrics Metrics { get; set; } = new();

        /// <summary>
        /// Converts the analysis results to YAML format
        /// </summary>
        public override string ToString()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            return serializer.Serialize(this);
        }
    }

    /// <summary>
    /// Contains detailed information about a single component in the codebase
    /// </summary>
    public class ComponentInfo
    {
        /// <summary>
        /// Name of the component
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// File path where the component is defined
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Cognitive complexity score of the component
        /// </summary>
        public double CognitiveComplexity { get; set; }

        /// <summary>
        /// List of component dependencies
        /// </summary>
        public List<string> Dependencies { get; set; } = new();

        /// <summary>
        /// List of component responsibilities
        /// </summary>
        public List<string> Responsibilities { get; set; } = new();

        /// <summary>
        /// List of methods defined in the component
        /// </summary>
        public List<MethodInfo> Methods { get; set; } = new();

        /// <summary>
        /// Component maintainability metrics
        /// </summary>
        public MaintainabilityMetrics Maintainability { get; set; } = new();
    }

    /// <summary>
    /// Represents a dependency relationship between two components
    /// </summary>
    public class DependencyInfo
    {
        /// <summary>
        /// Component that has the dependency
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Component that is depended upon
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Type of dependency (e.g., "inheritance", "composition", "usage")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Strength of the dependency (0-1)
        /// </summary>
        public double Strength { get; set; }

        /// <summary>
        /// Whether this is a direct or indirect dependency
        /// </summary>
        public bool IsDirectDependency { get; set; } = true;
    }

    /// <summary>
    /// Contains information about methods within a component
    /// </summary>
    public class MethodInfo
    {
        /// <summary>
        /// Name of the method
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Method's cognitive complexity score
        /// </summary>
        public double Complexity { get; set; }

        /// <summary>
        /// Number of parameters
        /// </summary>
        public int ParameterCount { get; set; }

        /// <summary>
        /// Return type of the method
        /// </summary>
        public string ReturnType { get; set; }

        /// <summary>
        /// List of method parameter types
        /// </summary>
        public List<string> ParameterTypes { get; set; } = new();
    }

    /// <summary>
    /// Contains overall metrics about the analyzed codebase
    /// </summary>
    public class CodebaseMetrics
    {
        /// <summary>
        /// Total number of components analyzed
        /// </summary>
        public int ComponentCount { get; set; }

        /// <summary>
        /// Total number of dependencies found
        /// </summary>
        public int DependencyCount { get; set; }

        /// <summary>
        /// Average cognitive complexity across all components
        /// </summary>
        public double AverageComplexity { get; set; }

        /// <summary>
        /// Number of high-complexity components (above threshold)
        /// </summary>
        public int HighComplexityCount { get; set; }

        /// <summary>
        /// Total number of methods across all components
        /// </summary>
        public int TotalMethodCount { get; set; }

        /// <summary>
        /// Total lines of code analyzed
        /// </summary>
        public int TotalLinesOfCode { get; set; }
    }

    /// <summary>
    /// Contains maintainability metrics for a component
    /// </summary>
    public class MaintainabilityMetrics
    {
        /// <summary>
        /// Cyclomatic complexity score
        /// </summary>
        public double CyclomaticComplexity { get; set; }

        /// <summary>
        /// Depth of inheritance tree
        /// </summary>
        public int InheritanceDepth { get; set; }

        /// <summary>
        /// Number of public methods
        /// </summary>
        public int PublicMethodCount { get; set; }

        /// <summary>
        /// Number of fields/properties
        /// </summary>
        public int MemberCount { get; set; }

        /// <summary>
        /// Lines of code in the component
        /// </summary>
        public int LinesOfCode { get; set; }

        /// <summary>
        /// Comment coverage percentage
        /// </summary>
        public double CommentCoverage { get; set; }
    }
}