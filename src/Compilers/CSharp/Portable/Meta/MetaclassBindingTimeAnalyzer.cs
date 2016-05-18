using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class MetaclassBindingTimeAnalyzer : StaticOnlyBindingTimeAnalyzer
    {
        private readonly SourceMemberMethodSymbol _applicationMethod;

        public MetaclassBindingTimeAnalyzer(CSharpCompilation compilation, DiagnosticBag diagnostics, Location sourceLocation, SourceMemberMethodSymbol applicationMethod)
            : base(compilation, diagnostics, sourceLocation)
        {
            _applicationMethod = applicationMethod;
        }

        public override BindingTimeAnalysisResult VisitCall(BoundCall node, BindingTimeAnalyzerFlags flags)
        {
            MethodSymbol method = node.Method;
            if (method == Compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ApplyDecorator))
            {
                ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, false);
                return (argumentsResults[0].BindingTime == BindingTime.Dynamic || argumentsResults[1].BindingTime == BindingTime.Dynamic)
                        ? new BindingTimeAnalysisResult(BindingTime.Dynamic)
                        : new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
            }

            return base.VisitCall(node, flags);
        }

        public override BindingTimeAnalysisResult VisitObjectCreationExpression(BoundObjectCreationExpression node, BindingTimeAnalyzerFlags flags)
        {
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            if (node.Type.IsDerivedFrom(Compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Decorator), false, ref useSiteDiagnostics))
            {
                // We can handle the compile-time execution of decorator instantiations, as long as they use the default constructor and have no property/field initialization
                if (!node.Arguments.IsEmpty || node.InitializerExpressionOpt != null)
                {
                    Error(ErrorCode.ERR_DecoratorWithArguments, node.Syntax.Location);
                    throw new BindingTimeAnalysisException();
                }
                else
                {
                    return new BindingTimeAnalysisResult(BindingTime.StaticComplexValue);
                }
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
    }
}
