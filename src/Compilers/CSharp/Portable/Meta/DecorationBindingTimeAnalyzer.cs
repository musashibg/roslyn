using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class DecorationBindingTimeAnalyzer : BaseBindingTimeAnalyzer
    {
        private readonly SourceMemberMethodSymbol _decoratorMethod;

        public DecorationBindingTimeAnalyzer(CSharpCompilation compilation, DiagnosticBag diagnostics, Location sourceLocation, SourceMemberMethodSymbol decoratorMethod)
            : base(compilation, diagnostics, sourceLocation)
        {
            _decoratorMethod = decoratorMethod;
        }

        public override BindingTimeAnalysisResult VisitCall(BoundCall node, BindingTimeAnalyzerFlags flags)
        {
            if (CheckIsSpliceLocation(node) || CheckIsBaseDecoratorMethodCall(node))
            {
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }

            return base.VisitCall(node, flags);
        }

        public bool PerformAnalysis()
        {
            Debug.Assert(_decoratorMethod.EarlyBoundBody != null);
            BoundBlock decoratorBody = _decoratorMethod.EarlyBoundBody;
            return PerformAnalysis(decoratorBody);
        }

        protected override ImmutableDictionary<Symbol, BindingTime> GetInitialVariableBindingTimes()
        {
            Debug.Assert(_decoratorMethod.DecoratorMethodVariableTypes != null);
            // Build the most optimistic variable binding times configuration, where as many variables as possible have static binding times
            return _decoratorMethod.DecoratorMethodVariableTypes.ToImmutableDictionary(
                kv => kv.Key,
                kv =>
                {
                    if (kv.Value.Kind == ExtendedTypeKind.ArgumentArray)
                    {
                        // All argument array parameters and variables are initially assumed to be static argument arrays
                        return BindingTime.StaticArgumentArray;
                    }
                    else if (kv.Key == _decoratorMethod.Parameters[1] || kv.Key is RangeVariableSymbol)
                    {
                        // The thisObject parameter of the decorator method is initially dynamic, and so are all range variables
                        return BindingTime.Dynamic;
                    }
                    else
                    {
                        // The method parameter and all other variables are initialy assumed to be static
                        return MetaUtils.CheckIsSimpleStaticValueType(kv.Value.OrdinaryType, Compilation)
                                ? BindingTime.StaticSimpleValue
                                : BindingTime.StaticComplexValue;
                    }
                });
        }

        protected override bool IsGuaranteedStaticSymbol(Symbol symbol)
        {
            return symbol.Kind == SymbolKind.Parameter && _decoratorMethod.Parameters.Contains((ParameterSymbol)symbol);
        }

        private static bool CheckIsSpecificParameter(BoundExpression node, ParameterSymbol parameter)
        {
            while (node.Kind == BoundKind.Conversion)
            {
                node = ((BoundConversion)node).Operand;
            }
            return node.Kind == BoundKind.Parameter && ((BoundParameter)node).ParameterSymbol == parameter;
        }

        private bool CheckIsSpliceLocation(BoundCall call)
        {
            MethodSymbol method = call.Method;
            if (call.Method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__Invoke))
            {
                // This is a call to MethodBase.Invoke(object obj, object[] parameters)
                if (call.ReceiverOpt != null
                    && CheckIsSpecificParameter(call.ReceiverOpt, _decoratorMethod.Parameters[0])
                    && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[1])
                    && (call.Arguments[1].Kind == BoundKind.Parameter || call.Arguments[1].Kind == BoundKind.Local))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        private bool CheckIsBaseDecoratorMethodCall(BoundCall call)
        {
            MethodSymbol method = call.Method;
            if (method.Name != _decoratorMethod.Name
                || method.ParameterCount != _decoratorMethod.ParameterCount)
            {
                return false;
            }

            for (int i = 0; i < method.ParameterCount; i++)
            {
                ParameterSymbol methodParameter = method.Parameters[i];
                ParameterSymbol decoratorMethodParameter = _decoratorMethod.Parameters[i];
                if (methodParameter.Type != decoratorMethodParameter.Type
                    || methodParameter.RefKind != decoratorMethodParameter.RefKind)
                {
                    return false;
                }
            }

            BoundExpression receiverOpt = call.ReceiverOpt;
            return receiverOpt != null && receiverOpt.Kind == BoundKind.BaseReference;
        }
    }
}
