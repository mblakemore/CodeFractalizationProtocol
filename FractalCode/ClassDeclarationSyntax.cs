using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FractalCode.Analysis
{
    /// <summary>
    /// Represents and analyzes class declarations in the Code Fractalization system.
    /// Provides detailed analysis of class structure, complexity, and relationships.
    /// </summary>
    public class ClassAnalyzer
    {
        private readonly ClassDeclarationSyntax _node;
        private readonly SemanticModel _semanticModel;
        private readonly Dictionary<string, double> _metricCache;

        /// <summary>
        /// Gets the identifier of the class
        /// </summary>
        public SyntaxToken Identifier { get; }

        /// <summary>
        /// Gets the base type list of the class
        /// </summary>
        public BaseListSyntax BaseList { get; }

        /// <summary>
        /// Gets the modifiers applied to the class
        /// </summary>
        public SyntaxTokenList Modifiers { get; }

        /// <summary>
        /// Gets the documentation comments for the class
        /// </summary>
        public SyntaxTrivia DocumentationComments { get; }

        /// <summary>
        /// Gets the cognitive complexity score of the class
        /// </summary>
        public double CognitiveComplexity => CalculateCognitiveComplexity();

        /// <summary>
        /// Initializes a new instance of ClassAnalyzer
        /// </summary>
        /// <param name="node">The syntax node representing the class</param>
        /// <param name="semanticModel">The semantic model for additional analysis</param>
        public ClassAnalyzer(ClassDeclarationSyntax node, SemanticModel semanticModel = null)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _semanticModel = semanticModel;
            _metricCache = new Dictionary<string, double>();

            Identifier = node.Identifier;
            BaseList = node.BaseList;
            Modifiers = node.Modifiers;
            DocumentationComments = GetDocumentationComments(node);
        }

        /// <summary>
        /// Gets all descendant nodes of a specific type from the class
        /// </summary>
        /// <typeparam name="T">Type of nodes to retrieve</typeparam>
        /// <returns>Enumerable of matching nodes</returns>
        public IEnumerable<T> DescendantNodes<T>() where T : SyntaxNode
        {
            return _node.DescendantNodes().OfType<T>();
        }

        /// <summary>
        /// Analyzes the class's dependencies and their relationships
        /// </summary>
        /// <returns>Dictionary of dependency analysis results</returns>
        public Dictionary<string, object> AnalyzeDependencies()
        {
            var dependencies = new Dictionary<string, object>();

            // Analyze inheritance dependencies
            if (BaseList != null)
            {
                dependencies["inheritance"] = BaseList.Types.Select(t => t.ToString()).ToList();
            }

            // Analyze field dependencies
            var fieldDependencies = DescendantNodes<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Select(v => new
                {
                    Name = v.Identifier.ToString(),
                    Type = v.Parent.Parent.GetFirstToken().ValueText
                })
                .ToList();
            dependencies["fields"] = fieldDependencies;

            // Analyze method dependencies
            var methodDependencies = DescendantNodes<MethodDeclarationSyntax>()
                .Select(m => new
                {
                    Name = m.Identifier.ToString(),
                    ReturnType = m.ReturnType.ToString(),
                    Parameters = m.ParameterList.Parameters.Select(p => p.Type.ToString()).ToList()
                })
                .ToList();
            dependencies["methods"] = methodDependencies;

            return dependencies;
        }

        /// <summary>
        /// Calculates metrics related to class complexity
        /// </summary>
        /// <returns>Dictionary of complexity metrics</returns>
        public Dictionary<string, double> CalculateComplexityMetrics()
        {
            var metrics = new Dictionary<string, double>
            {
                ["cognitive_complexity"] = CognitiveComplexity,
                ["inheritance_depth"] = CalculateInheritanceDepth(),
                ["method_complexity"] = CalculateMethodComplexity(),
                ["dependency_complexity"] = CalculateDependencyComplexity(),
                ["coupling_factor"] = CalculateCouplingFactor()
            };

            return metrics;
        }

        /// <summary>
        /// Analyzes the class's responsibility patterns
        /// </summary>
        /// <returns>List of identified responsibilities</returns>
        public List<string> AnalyzeResponsibilities()
        {
            var responsibilities = new List<string>();

            // Check interface implementations
            if (BaseList != null)
            {
                foreach (var type in BaseList.Types)
                {
                    if (type.ToString().StartsWith("I"))
                    {
                        responsibilities.Add($"Implements {type} interface");
                    }
                }
            }

            // Analyze method responsibilities
            foreach (var method in DescendantNodes<MethodDeclarationSyntax>())
            {
                // Check documentation comments
                var docs = GetDocumentationComments(method);
                if (!string.IsNullOrEmpty(docs.ToString()))
                {
                    responsibilities.Add($"Handles {method.Identifier}: {docs.ToString().Trim()}");
                }

                // Check attributes
                foreach (var attributeList in method.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        responsibilities.Add($"Provides {attribute.Name} capability through {method.Identifier}");
                    }
                }
            }

            // Analyze property responsibilities
            foreach (var property in DescendantNodes<PropertyDeclarationSyntax>())
            {
                var docs = GetDocumentationComments(property);
                if (!string.IsNullOrEmpty(docs.ToString()))
                {
                    responsibilities.Add($"Manages {property.Identifier}: {docs.ToString().Trim()}");
                }
            }

            return responsibilities.Distinct().ToList();
        }

        /// <summary>
        /// Suggests potential refactoring opportunities for the class
        /// </summary>
        /// <returns>List of refactoring suggestions</returns>
        public List<string> SuggestRefactoring()
        {
            var suggestions = new List<string>();

            // Check complexity-based refactoring needs
            if (CognitiveComplexity > 0.65)
            {
                suggestions.Add($"High cognitive complexity ({CognitiveComplexity:F2}). Consider splitting class.");
            }

            // Check method count
            var methodCount = DescendantNodes<MethodDeclarationSyntax>().Count();
            if (methodCount > 10)
            {
                suggestions.Add($"Large number of methods ({methodCount}). Consider extracting related methods into new class.");
            }

            // Check responsibility clustering
            var responsibilities = AnalyzeResponsibilities();
            var clusters = responsibilities
                .GroupBy(r => r.Split(' ')[0])
                .Where(g => g.Count() >= 3);

            foreach (var cluster in clusters)
            {
                suggestions.Add($"Consider extracting {cluster.Key}-related responsibilities into separate component.");
            }

            // Check inheritance depth
            var inheritanceDepth = CalculateInheritanceDepth();
            if (inheritanceDepth > 3)
            {
                suggestions.Add($"Deep inheritance hierarchy ({inheritanceDepth} levels). Consider composition over inheritance.");
            }

            return suggestions;
        }

        private double CalculateCognitiveComplexity()
        {
            if (_metricCache.TryGetValue("cognitive_complexity", out double cached))
                return cached;

            double complexity = 0;

            // Method complexity
            complexity += DescendantNodes<MethodDeclarationSyntax>()
                .Sum(m => CalculateMethodNodeComplexity(m));

            // Inheritance complexity
            if (BaseList != null)
            {
                complexity += BaseList.Types.Count * 0.1;
            }

            // Field complexity
            complexity += DescendantNodes<FieldDeclarationSyntax>().Count() * 0.05;

            // Property complexity
            complexity += DescendantNodes<PropertyDeclarationSyntax>().Count() * 0.05;

            // Cache and return normalized complexity
            var normalized = Math.Min(complexity, 1.0);
            _metricCache["cognitive_complexity"] = normalized;
            return normalized;
        }

        private double CalculateMethodNodeComplexity(MethodDeclarationSyntax method)
        {
            double complexity = 0;

            // Control flow complexity
            complexity += method.DescendantNodes().OfType<IfStatementSyntax>().Count() * 0.1;
            complexity += method.DescendantNodes().OfType<ForStatementSyntax>().Count() * 0.1;
            complexity += method.DescendantNodes().OfType<WhileStatementSyntax>().Count() * 0.1;
            complexity += method.DescendantNodes().OfType<SwitchStatementSyntax>().Count() * 0.15;

            // Parameter complexity
            complexity += method.ParameterList.Parameters.Count * 0.05;

            // Local variable complexity
            complexity += method.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Count() * 0.05;

            return complexity;
        }

        private double CalculateInheritanceDepth()
        {
            if (_metricCache.TryGetValue("inheritance_depth", out double cached))
                return cached;

            double depth = 0;
            if (BaseList != null)
            {
                depth = BaseList.Types.Count;
                if (_semanticModel != null)
                {
                    foreach (var type in BaseList.Types)
                    {
                        var symbol = _semanticModel.GetSymbolInfo(type.Type).Symbol as INamedTypeSymbol;
                        while (symbol != null && symbol.BaseType != null)
                        {
                            depth += 0.5; // Weight indirect inheritance less
                            symbol = symbol.BaseType;
                        }
                    }
                }
            }

            _metricCache["inheritance_depth"] = depth;
            return depth;
        }

        private double CalculateMethodComplexity()
        {
            if (_metricCache.TryGetValue("method_complexity", out double cached))
                return cached;

            var methods = DescendantNodes<MethodDeclarationSyntax>();
            double totalComplexity = methods.Sum(m => CalculateMethodNodeComplexity(m));
            double averageComplexity = methods.Any() ? totalComplexity / methods.Count() : 0;

            _metricCache["method_complexity"] = averageComplexity;
            return averageComplexity;
        }

        private double CalculateDependencyComplexity()
        {
            if (_metricCache.TryGetValue("dependency_complexity", out double cached))
                return cached;

            var dependencies = AnalyzeDependencies();
            double complexity = 0;

            // Weight different types of dependencies
            if (dependencies.ContainsKey("inheritance"))
            {
                complexity += ((List<string>)dependencies["inheritance"]).Count * 0.3;
            }

            var fields = (List<object>)dependencies["fields"];
            complexity += fields.Count * 0.2;

            var methods = (List<object>)dependencies["methods"];
            complexity += methods.Count * 0.1;

            _metricCache["dependency_complexity"] = Math.Min(complexity, 1.0);
            return _metricCache["dependency_complexity"];
        }

        private double CalculateCouplingFactor()
        {
            if (_metricCache.TryGetValue("coupling_factor", out double cached))
                return cached;

            var dependencies = AnalyzeDependencies();
            var uniqueDependencies = new HashSet<string>();

            // Collect unique external dependencies
            if (dependencies.ContainsKey("inheritance"))
            {
                foreach (var dep in (List<string>)dependencies["inheritance"])
                {
                    uniqueDependencies.Add(dep);
                }
            }

            var fields = (List<object>)dependencies["fields"];
            foreach (var field in fields)
            {
                uniqueDependencies.Add(((dynamic)field).Type.ToString());
            }

            var methods = (List<object>)dependencies["methods"];
            foreach (var method in methods)
            {
                uniqueDependencies.Add(((dynamic)method).ReturnType.ToString());
                foreach (var param in ((dynamic)method).Parameters)
                {
                    uniqueDependencies.Add(param.ToString());
                }
            }

            // Calculate coupling factor
            double factor = uniqueDependencies.Count * 0.1;
            _metricCache["coupling_factor"] = Math.Min(factor, 1.0);
            return _metricCache["coupling_factor"];
        }

        private SyntaxTrivia GetDocumentationComments(SyntaxNode node)
        {
            var triviaList = node.GetLeadingTrivia();
            foreach (var trivia in triviaList)
            {
                if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                    trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    return trivia;
                }
            }
            return default;
        }
    }
}