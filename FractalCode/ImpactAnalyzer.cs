using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using FractalCode.Core.Models;
using FractalCode.Contracts;
using FractalCode.Analysis;
//using QuickGraph;
//using QuickGraph.Algorithms;
using YamlDotNet.Serialization.NamingConventions;

namespace FractalCode.Impact
{
    public class ImpactAnalysisResult
    {
        public Dictionary<string, double> ImpactScores { get; set; } = new();
        public List<RiskArea> RiskAreas { get; set; } = new();
        public List<string> SuggestedMitigations { get; set; } = new();
        public Dictionary<string, List<string>> AffectedComponents { get; set; } = new();

        public override string ToString()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            return serializer.Serialize(this);
        }
    }

    public class RiskArea
    {
        public string Component { get; set; }
        public string RiskType { get; set; }
        public double RiskScore { get; set; }
        public string Description { get; set; }
        public List<string> AffectedContracts { get; set; } = new();
    }

    public class ChangeSpecification
    {
        public string Component { get; set; }
        public string ChangeType { get; set; }
        public Dictionary<string, object> Changes { get; set; }
        public List<string> AffectedContracts { get; set; }
        public Dictionary<string, object> ExpectedImpact { get; set; }
    }

    public interface IImpactAnalyzer
    {
        Task<ImpactAnalysisResult> AnalyzeChangeImpactAsync(string changeSpecPath);
        Task<ImpactAnalysisResult> ValidateChangeAsync(string changeSpecPath);
        Task<Dictionary<string, double>> CalculateImpactScoresAsync(ChangeSpecification change);
    }

    public class ImpactAnalyzer : IImpactAnalyzer
    {
        private readonly ILogger<ImpactAnalyzer> _logger;
        private readonly IContractValidator _contractValidator;
        private readonly ICodeAnalyzer _codeAnalyzer;
        private readonly IDeserializer _yamlDeserializer;

        // Impact threshold constants
        private const double HIGH_IMPACT_THRESHOLD = 0.7;
        private const double MEDIUM_IMPACT_THRESHOLD = 0.4;
        private const double RISK_THRESHOLD = 0.6;

        public ImpactAnalyzer(
            ILogger<ImpactAnalyzer> logger,
            IContractValidator contractValidator,
            ICodeAnalyzer codeAnalyzer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _contractValidator = contractValidator ?? throw new ArgumentNullException(nameof(contractValidator));
            _codeAnalyzer = codeAnalyzer ?? throw new ArgumentNullException(nameof(codeAnalyzer));

            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public async Task<ImpactAnalysisResult> AnalyzeChangeImpactAsync(string changeSpecPath)
        {
            try
            {
                _logger.LogInformation($"Analyzing change impact for specification: {changeSpecPath}");

                // Load and parse change specification
                var changeSpec = await LoadChangeSpecificationAsync(changeSpecPath);

                // Calculate impact scores
                var impactScores = await CalculateImpactScoresAsync(changeSpec);

                // Identify risk areas
                var riskAreas = IdentifyRiskAreas(changeSpec, impactScores);

                // Generate mitigations
                var mitigations = GenerateMitigationStrategies(riskAreas);

                // Identify affected components
                var affectedComponents = await IdentifyAffectedComponentsAsync(changeSpec, impactScores);

                return new ImpactAnalysisResult
                {
                    ImpactScores = impactScores,
                    RiskAreas = riskAreas,
                    SuggestedMitigations = mitigations,
                    AffectedComponents = affectedComponents
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing change impact");
                throw;
            }
        }

        public async Task<ImpactAnalysisResult> ValidateChangeAsync(string changeSpecPath)
        {
            try
            {
                _logger.LogInformation($"Validating change specification: {changeSpecPath}");

                // Load and validate change specification
                var changeSpec = await LoadChangeSpecificationAsync(changeSpecPath);

                // Analyze impact
                var impactResult = await AnalyzeChangeImpactAsync(changeSpecPath);

                // Validate against contracts
                await ValidateContractComplianceAsync(changeSpec);

                // Validate against expected impact
                ValidateExpectedImpact(changeSpec, impactResult);

                return impactResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating change");
                throw;
            }
        }

        public async Task<Dictionary<string, double>> CalculateImpactScoresAsync(ChangeSpecification change)
        {
            try
            {
                // Build dependency graph
                var graph = await BuildDependencyGraphAsync(change.Component);

                // Calculate impact probabilities using weighted PageRank
                var algorithm = new PageRankAlgorithm<string, Edge<string>>(graph);
                algorithm.Compute();

                // Convert PageRank scores to impact scores
                var impactScores = new Dictionary<string, double>();
                foreach (var vertex in graph.Vertices)
                {
                    var baseScore = algorithm.PageRanks[vertex];
                    var adjustedScore = AdjustImpactScore(baseScore, change, vertex);
                    impactScores[vertex] = adjustedScore;
                }

                return impactScores;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating impact scores");
                throw;
            }
        }

        private async Task<ChangeSpecification> LoadChangeSpecificationAsync(string path)
        {
            try
            {
                var content = await File.ReadAllTextAsync(path);
                return _yamlDeserializer.Deserialize<ChangeSpecification>(content);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading change specification: {ex.Message}", ex);
            }
        }

        private async Task<AdjacencyGraph<string, Edge<string>>> BuildDependencyGraphAsync(string rootComponent)
        {
            var graph = new AdjacencyGraph<string, Edge<string>>();

            // Analyze codebase to get dependencies
            var analysis = await _codeAnalyzer.AnalyzeCodebaseAsync(Directory.GetCurrentDirectory());

            // Add vertices
            foreach (var component in analysis.Components)
            {
                graph.AddVertex(component.Name);
            }

            // Add edges
            foreach (var component in analysis.Components)
            {
                foreach (var dependency in component.Dependencies)
                {
                    graph.AddEdge(new Edge<string>(component.Name, dependency));
                }
            }

            return graph;
        }

        private double AdjustImpactScore(double baseScore, ChangeSpecification change, string component)
        {
            double adjustedScore = baseScore;

            // Adjust based on change type
            switch (change.ChangeType.ToLower())
            {
                case "contract":
                    adjustedScore *= 1.5; // Contract changes have higher impact
                    break;
                case "implementation":
                    adjustedScore *= 1.2;
                    break;
                case "resource":
                    adjustedScore *= 1.3;
                    break;
            }

            // Adjust based on contract involvement
            if (change.AffectedContracts?.Contains(component) == true)
            {
                adjustedScore *= 1.4;
            }

            // Normalize score
            return Math.Min(adjustedScore, 1.0);
        }

        private List<RiskArea> IdentifyRiskAreas(ChangeSpecification change, Dictionary<string, double> impactScores)
        {
            var riskAreas = new List<RiskArea>();

            foreach (var (component, score) in impactScores)
            {
                if (score >= RISK_THRESHOLD)
                {
                    var riskArea = new RiskArea
                    {
                        Component = component,
                        RiskScore = score,
                        RiskType = DetermineRiskType(change, component, score),
                        Description = GenerateRiskDescription(change, component, score),
                        AffectedContracts = change.AffectedContracts?.Where(c => c.StartsWith(component)).ToList() ?? new List<string>()
                    };

                    riskAreas.Add(riskArea);
                }
            }

            return riskAreas;
        }

        private string DetermineRiskType(ChangeSpecification change, string component, double score)
        {
            if (change.AffectedContracts?.Contains(component) == true)
            {
                return "ContractCompliance";
            }
            else if (score >= HIGH_IMPACT_THRESHOLD)
            {
                return "HighImpact";
            }
            else if (score >= MEDIUM_IMPACT_THRESHOLD)
            {
                return "MediumImpact";
            }
            return "LowImpact";
        }

        private string GenerateRiskDescription(ChangeSpecification change, string component, double score)
        {
            if (score >= HIGH_IMPACT_THRESHOLD)
            {
                return $"High risk of breaking changes affecting {component}";
            }
            else if (score >= MEDIUM_IMPACT_THRESHOLD)
            {
                return $"Potential indirect effects on {component}";
            }
            return $"Minor impact possible on {component}";
        }

        private List<string> GenerateMitigationStrategies(List<RiskArea> riskAreas)
        {
            var mitigations = new List<string>();

            foreach (var risk in riskAreas)
            {
                switch (risk.RiskType)
                {
                    case "ContractCompliance":
                        mitigations.Add($"Implement compatibility layer for {risk.Component}");
                        mitigations.Add($"Add contract validation tests for {risk.Component}");
                        break;

                    case "HighImpact":
                        mitigations.Add($"Phase implementation for {risk.Component}");
                        mitigations.Add($"Increase test coverage for {risk.Component}");
                        mitigations.Add($"Prepare rollback procedure for {risk.Component}");
                        break;

                    case "MediumImpact":
                        mitigations.Add($"Monitor {risk.Component} during deployment");
                        mitigations.Add($"Add performance tests for {risk.Component}");
                        break;
                }
            }

            return mitigations.Distinct().ToList();
        }

        private async Task<Dictionary<string, List<string>>> IdentifyAffectedComponentsAsync(
            ChangeSpecification change,
            Dictionary<string, double> impactScores)
        {
            var affected = new Dictionary<string, List<string>>();

            // Group by impact level
            affected["high"] = impactScores
                .Where(s => s.Value >= HIGH_IMPACT_THRESHOLD)
                .Select(s => s.Key)
                .ToList();

            affected["medium"] = impactScores
                .Where(s => s.Value >= MEDIUM_IMPACT_THRESHOLD && s.Value < HIGH_IMPACT_THRESHOLD)
                .Select(s => s.Key)
                .ToList();

            affected["low"] = impactScores
                .Where(s => s.Value < MEDIUM_IMPACT_THRESHOLD)
                .Select(s => s.Key)
                .ToList();

            // Add contract-specific impacts
            if (change.AffectedContracts?.Any() == true)
            {
                affected["contracts"] = change.AffectedContracts;
            }

            return affected;
        }

        private async Task ValidateContractComplianceAsync(ChangeSpecification change)
        {
            if (change.AffectedContracts?.Any() == true)
            {
                foreach (var contract in change.AffectedContracts)
                {
                    var validation = await _contractValidator.ValidateContractAsync(
                        contract,
                        "interface" // Default to interface contract type
                    );

                    if (!validation.IsValid)
                    {
                        throw new InvalidOperationException(
                            $"Contract validation failed for {contract}: " +
                            string.Join(", ", validation.Errors)
                        );
                    }
                }
            }
        }

        private void ValidateExpectedImpact(ChangeSpecification change, ImpactAnalysisResult actualImpact)
        {
            if (change.ExpectedImpact != null)
            {
                // Compare actual impact scores with expected impact
                foreach (var (component, expectedImpact) in change.ExpectedImpact)
                {
                    if (actualImpact.ImpactScores.TryGetValue(component, out double actualScore))
                    {
                        var expectedScore = Convert.ToDouble(expectedImpact);
                        if (Math.Abs(actualScore - expectedScore) > 0.2) // 20% tolerance
                        {
                            _logger.LogWarning(
                                $"Impact mismatch for {component}: " +
                                $"Expected {expectedScore:F2}, Actual {actualScore:F2}"
                            );
                        }
                    }
                }
            }
        }
    }
    /// <summary>
    /// Represents a directed edge in a graph
    /// </summary>
    public class Edge<T>
    {
        public T Source { get; }
        public T Target { get; }
        public double Weight { get; set; }

        public Edge(T source, T target, double weight = 1.0)
        {
            Source = source;
            Target = target;
            Weight = weight;
        }
    }

    /// <summary>
    /// Represents a directed graph using adjacency lists
    /// </summary>
    public class AdjacencyGraph<TVertex, TEdge> where TEdge : Edge<TVertex>
    {
        private readonly Dictionary<TVertex, List<TEdge>> _adjacencyList;
        private readonly HashSet<TVertex> _vertices;

        public IEnumerable<TVertex> Vertices => _vertices;
        public IEnumerable<TEdge> Edges => _adjacencyList.Values.SelectMany(x => x);

        public AdjacencyGraph()
        {
            _adjacencyList = new Dictionary<TVertex, List<TEdge>>();
            _vertices = new HashSet<TVertex>();
        }

        public void AddVertex(TVertex vertex)
        {
            if (!_vertices.Contains(vertex))
            {
                _vertices.Add(vertex);
                _adjacencyList[vertex] = new List<TEdge>();
            }
        }

        public void AddEdge(TEdge edge)
        {
            AddVertex(edge.Source);
            AddVertex(edge.Target);
            _adjacencyList[edge.Source].Add(edge);
        }

        public IEnumerable<TEdge> OutEdges(TVertex vertex)
        {
            return _adjacencyList.TryGetValue(vertex, out var edges) ? edges : Enumerable.Empty<TEdge>();
        }

        public IEnumerable<TVertex> Neighbors(TVertex vertex)
        {
            return OutEdges(vertex).Select(e => e.Target);
        }
    }

    /// <summary>
    /// Implements the PageRank algorithm for graph analysis
    /// </summary>
    public class PageRankAlgorithm<TVertex, TEdge> where TEdge : Edge<TVertex>
    {
        private readonly AdjacencyGraph<TVertex, TEdge> _graph;
        private readonly double _dampingFactor;
        private readonly int _maxIterations;
        private readonly double _tolerance;

        public Dictionary<TVertex, double> PageRanks { get; private set; }

        public PageRankAlgorithm(
            AdjacencyGraph<TVertex, TEdge> graph,
            double dampingFactor = 0.85,
            int maxIterations = 100,
            double tolerance = 0.0001)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _dampingFactor = dampingFactor;
            _maxIterations = maxIterations;
            _tolerance = tolerance;
            PageRanks = new Dictionary<TVertex, double>();
        }

        public void Compute()
        {
            // Initialize PageRanks
            double initialRank = 1.0 / _graph.Vertices.Count();
            PageRanks = _graph.Vertices.ToDictionary(v => v, v => initialRank);

            for (int iteration = 0; iteration < _maxIterations; iteration++)
            {
                var newRanks = new Dictionary<TVertex, double>();
                double totalDiff = 0;

                foreach (var vertex in _graph.Vertices)
                {
                    double incomingRank = 0;
                    foreach (var incomingEdge in _graph.Edges.Where(e => e.Target.Equals(vertex)))
                    {
                        var source = incomingEdge.Source;
                        var sourceOutDegree = _graph.OutEdges(source).Count();
                        if (sourceOutDegree > 0)
                        {
                            incomingRank += (PageRanks[source] * incomingEdge.Weight) / sourceOutDegree;
                        }
                    }

                    double newRank = (1 - _dampingFactor) + _dampingFactor * incomingRank;
                    newRanks[vertex] = newRank;
                    totalDiff += Math.Abs(newRank - PageRanks[vertex]);
                }

                PageRanks = newRanks;

                // Check for convergence
                if (totalDiff < _tolerance)
                {
                    break;
                }
            }

            // Normalize ranks
            double sum = PageRanks.Values.Sum();
            foreach (var vertex in PageRanks.Keys.ToList())
            {
                PageRanks[vertex] /= sum;
            }
        }
    }
}