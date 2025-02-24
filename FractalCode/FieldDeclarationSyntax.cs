using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
//using Microsoft.CodeAnalysis.FindSymbols;

namespace FractalCode.Analysis
{
    /// <summary>
    /// Represents and analyzes field declarations in the Code Fractalization system.
    /// Provides detailed analysis of field structure, usage, and relationships.
    /// </summary>
    public class FieldAnalyzer
    {
        private readonly FieldDeclarationSyntax _node;
        private readonly SemanticModel _semanticModel;
        private readonly Dictionary<string, object> _analysisCache;

        /// <summary>
        /// Gets the declaration containing the field variables
        /// </summary>
        public VariableDeclarationSyntax Declaration { get; }

        /// <summary>
        /// Gets the modifiers applied to the field
        /// </summary>
        public SyntaxTokenList Modifiers { get; }

        /// <summary>
        /// Gets the documentation comments for the field
        /// </summary>
        public SyntaxTrivia DocumentationComments { get; }

        /// <summary>
        /// Gets the attributes applied to the field
        /// </summary>
        public IReadOnlyList<AttributeListSyntax> AttributeLists { get; }

        /// <summary>
        /// Initializes a new instance of FieldAnalyzer
        /// </summary>
        /// <param name="node">The syntax node representing the field</param>
        /// <param name="semanticModel">The semantic model for additional analysis</param>
        public FieldAnalyzer(FieldDeclarationSyntax node, SemanticModel semanticModel = null)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _semanticModel = semanticModel;
            _analysisCache = new Dictionary<string, object>();

            Declaration = node.Declaration;
            Modifiers = node.Modifiers;
            DocumentationComments = GetDocumentationComments(node);
            AttributeLists = node.AttributeLists.ToList();
        }

        /// <summary>
        /// Gets all variables declared in this field
        /// </summary>
        public IEnumerable<VariableDeclaratorSyntax> Variables => Declaration.Variables;

        /// <summary>
        /// Gets the type of the field
        /// </summary>
        public TypeSyntax Type => Declaration.Type;

        /// <summary>
        /// Analyzes the field's usage patterns and relationships
        /// </summary>
        public Dictionary<string, object> AnalyzeUsage()
        {
            if (_analysisCache.TryGetValue("usage", out var cached))
                return (Dictionary<string, object>)cached;

            var analysis = new Dictionary<string, object>();

            // Analyze direct references
            if (_semanticModel != null)
            {
                var symbol = _semanticModel.GetDeclaredSymbol(_node);
                if (symbol != null)
                {
                    // Get references using GetSymbolInfo instead of async method
                    var references = _node.SyntaxTree.GetRoot()
                        .DescendantNodes()
                        .Where(n => {
                            var symbolInfo = _semanticModel.GetSymbolInfo(n);
                            return symbolInfo.Symbol?.Equals(symbol) == true;
                        });

                    analysis["reference_count"] = references.Count();
                    analysis["reference_locations"] = references
                        .Select(r => r.GetLocation().GetLineSpan().StartLinePosition.Line)
                        .ToList();
                }
            }

            // Analyze access patterns
            analysis["access_patterns"] = AnalyzeAccessPatterns();

            // Analyze dependencies
            analysis["dependencies"] = AnalyzeDependencies();

            _analysisCache["usage"] = analysis;
            return analysis;
        }

        /// <summary>
        /// Analyzes the field's access patterns
        /// </summary>
        public Dictionary<string, object> AnalyzeAccessPatterns()
        {
            var patterns = new Dictionary<string, object>();

            // Determine access level
            patterns["access_level"] = GetAccessLevel();

            // Check for readonly/const
            patterns["is_readonly"] = Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
            patterns["is_const"] = Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));

            // Check for static
            patterns["is_static"] = Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

            // Check for backing field pattern
            patterns["is_backing_field"] = IsBackingField();

            // Check initialization
            patterns["has_initializer"] = Variables.Any(v => v.Initializer != null);

            return patterns;
        }

        /// <summary>
        /// Analyzes the field's dependencies
        /// </summary>
        public List<string> AnalyzeDependencies()
        {
            var dependencies = new List<string>();

            // Add type dependency
            dependencies.Add(Type.ToString());

            // Add generic type arguments if any
            if (Type is GenericNameSyntax genericType)
            {
                dependencies.AddRange(genericType.TypeArgumentList.Arguments
                    .Select(a => a.ToString()));
            }

            // Add dependencies from initializer
            foreach (var variable in Variables)
            {
                if (variable.Initializer != null)
                {
                    dependencies.AddRange(GetInitializerDependencies(variable.Initializer));
                }
            }

            return dependencies.Distinct().ToList();
        }

        private string GetAccessLevel()
        {
            if (Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return "public";
            if (Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) return "protected";
            if (Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))) return "internal";
            if (Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) return "private";
            return "private"; // default access level
        }

        private IEnumerable<string> GetInitializerDependencies(EqualsValueClauseSyntax initializer)
        {
            var dependencies = new List<string>();

            // Get type references from initializer
            var typeRefs = initializer.Value.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(n => n.Identifier.ToString());
            dependencies.AddRange(typeRefs);

            // Get member access dependencies
            var memberAccess = initializer.Value.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Select(m => m.ToString());
            dependencies.AddRange(memberAccess);

            return dependencies;
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

        private bool IsBackingField()
        {
            // Check naming pattern (e.g., _propertyName)
            var isBackingFieldName = Variables.Any(v =>
                v.Identifier.ToString().StartsWith("_") &&
                char.IsLower(v.Identifier.ToString()[1]));

            // Check if referenced only in property
            if (_semanticModel != null)
            {
                var symbol = _semanticModel.GetDeclaredSymbol(_node);
                if (symbol != null)
                {
                    // Get references using GetSymbolInfo
                    var references = _node.SyntaxTree.GetRoot()
                        .DescendantNodes()
                        .Where(n => {
                            var symbolInfo = _semanticModel.GetSymbolInfo(n);
                            return symbolInfo.Symbol?.Equals(symbol) == true;
                        });

                    var isOnlyInProperty = references.All(r =>
                        r.Ancestors()
                        .OfType<PropertyDeclarationSyntax>()
                        .Any());

                    return isBackingFieldName && isOnlyInProperty;
                }
            }

            return isBackingFieldName;
        }

        // ... [Rest of the methods remain unchanged] ...
    }
}