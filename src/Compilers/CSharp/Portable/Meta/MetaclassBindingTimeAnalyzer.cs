using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class MetaclassBindingTimeAnalyzer : StaticOnlyBindingTimeAnalyzer
    {
        private readonly SourceMemberMethodSymbol _applicationMethod;
        private readonly ImmutableDictionary<Symbol, BoundExpression> _metaclassArguments;

        public MetaclassBindingTimeAnalyzer(
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            Location sourceLocation,
            SourceMemberMethodSymbol applicationMethod,
            ImmutableDictionary<Symbol, BoundExpression> metaclassArguments)
            : base(compilation, diagnostics, sourceLocation)
        {
            _applicationMethod = applicationMethod;
            _metaclassArguments = metaclassArguments;
        }

        public override BindingTimeAnalysisResult VisitCall(BoundCall node, BindingTimeAnalyzerFlags flags)
        {
            MethodSymbol method = node.Method;
            if (method == Compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__AddTrait)
                || method.OriginalDefinition == Compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__AddTrait_T)
                || method == Compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ApplyDecorator))
            {
                ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, false);
                return argumentsResults.Any(r => r.BindingTime == BindingTime.Dynamic)
                        ? new BindingTimeAnalysisResult(BindingTime.Dynamic)
                        : new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
            }

            return base.VisitCall(node, flags);
        }

        public override BindingTimeAnalysisResult VisitFieldAccess(BoundFieldAccess node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
                if (strippedReceiver != null && (strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitMetaclassArgument(node, node.FieldSymbol, flags);
                }
            }

            return base.VisitFieldAccess(node, flags);
        }

        public override BindingTimeAnalysisResult VisitPropertyAccess(BoundPropertyAccess node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
                if (strippedReceiver != null && (strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitMetaclassArgument(node, node.PropertySymbol, flags);
                }
            }

            return base.VisitPropertyAccess(node, flags);
        }

        public override BindingTimeAnalysisResult VisitObjectCreationExpression(BoundObjectCreationExpression node, BindingTimeAnalyzerFlags flags)
        {
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            if (node.Type.IsDerivedFrom(Compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator), false, ref useSiteDiagnostics))
            {
                ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(
                    node.Arguments,
                    node.ArgumentRefKindsOpt,
                    flags | BindingTimeAnalyzerFlags.InDecoratorCreationExpression,
                    false);
                BindingTimeAnalysisResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, flags | BindingTimeAnalyzerFlags.InDecoratorCreationExpression);
                return new BindingTimeAnalysisResult(BindingTime.StaticComplexValue);
            }

            return base.VisitObjectCreationExpression(node, flags);
        }

        public bool PerformAnalysis()
        {
            Debug.Assert(_applicationMethod.EarlyBoundBody != null);
            BoundBlock applicationMethodBody = _applicationMethod.EarlyBoundBody;
            return PerformAnalysis(applicationMethodBody);
        }

        protected override ImmutableDictionary<Symbol, BindingTime> GetInitialVariableBindingTimes()
        {
            ImmutableHashSet<Symbol> methodVariables = ParametersAndVariablesWalker.GetParametersAndVariables(_applicationMethod);
            return methodVariables.ToImmutableDictionary(
                s => s,
                s =>
                {
                    if (s.Kind == SymbolKind.RangeVariable)
                    {
                        return BindingTime.Dynamic;
                    }

                    TypeSymbol type;
                    if (s.Kind == SymbolKind.Parameter)
                    {
                        type = ((ParameterSymbol)s).Type;
                    }
                    else
                    {
                        Debug.Assert(s.Kind == SymbolKind.Local);
                        type = ((LocalSymbol)s).Type;
                    }
                    return MetaUtils.CheckIsSimpleStaticValueType(type, Compilation)
                            ? BindingTime.StaticSimpleValue
                            : BindingTime.StaticComplexValue;
                });
        }

        private BindingTimeAnalysisResult VisitMetaclassArgument(BoundExpression node, Symbol metaclassMember, BindingTimeAnalyzerFlags flags)
        {
            BoundExpression meraclassArgument;
            if (_metaclassArguments.TryGetValue(metaclassMember, out meraclassArgument))
            {
                return Visit(meraclassArgument, flags | BindingTimeAnalyzerFlags.InMetaDecorationArgument);
            }
            else
            {
                // No value was assigned to the field/property manually or in the metaclass constructor, so it contains the default value for the type
                TypeSymbol type = node.Type;
                return (type.IsClassType() || MetaUtils.CheckIsSimpleStaticValueType(node.Type, Compilation))
                        ? new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue)
                        : new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }
        }
    }
}
