using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using Roslyn.Utilities;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class DecorationBindingTimeAnalyzer : BaseBindingTimeAnalyzer
    {
        private readonly DecoratedMemberKind _targetMemberKind;
        private readonly SourceMemberMethodSymbol _decoratorMethod;
        private readonly ImmutableDictionary<Symbol, BoundExpression> _decoratorArguments;

        public DecorationBindingTimeAnalyzer(
            CSharpCompilation compilation,
            DiagnosticBag diagnostics,
            CancellationToken cancellationToken,
            Location sourceLocation,
            DecoratedMemberKind targetMemberKind,
            SourceMemberMethodSymbol decoratorMethod,
            ImmutableDictionary<Symbol, BoundExpression> decoratorArguments)
            : base(compilation, diagnostics, cancellationToken, sourceLocation)
        {
            _targetMemberKind = targetMemberKind;
            _decoratorMethod = decoratorMethod;
            _decoratorArguments = decoratorArguments;
        }

        public override BindingTimeAnalysisResult VisitCall(BoundCall node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument)
                && (CheckIsSpliceLocation(node) || CheckIsBaseDecoratorMethodCall(node)))
            {
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }

            MethodSymbol method = node.Method;
            if (method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue)
                || method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue))
            {
                // We will transform calls to PropertyInfo.GetValue and PropertyInfo.SetValue to direct property accesses, so we do not need to force
                // the property variable to have dynamic binding time
                BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
                Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray);

                ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, true);

                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }

            return base.VisitCall(node, flags);
        }

        public override BindingTimeAnalysisResult VisitFieldAccess(BoundFieldAccess node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
                if (strippedReceiver != null &&(strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitDecoratorArgument(node, node.FieldSymbol, flags);
                }
            }

            return base.VisitFieldAccess(node, flags);
        }

        public override BindingTimeAnalysisResult VisitPropertyAccess(BoundPropertyAccess node, BindingTimeAnalyzerFlags flags)
        {
            if (!flags.HasFlag(BindingTimeAnalyzerFlags.InMetaDecorationArgument))
            {
                BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
                if (strippedReceiver != null &&(strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
                {
                    return VisitDecoratorArgument(node, node.PropertySymbol, flags);
                }
            }

            return base.VisitPropertyAccess(node, flags);
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
                    else if (kv.Key == _decoratorMethod.Parameters[1]
                             || ((_targetMemberKind == DecoratedMemberKind.IndexerSet || _targetMemberKind == DecoratedMemberKind.PropertySet) && kv.Key == _decoratorMethod.Parameters[2])
                             || kv.Key is RangeVariableSymbol)
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

        private BindingTimeAnalysisResult VisitDecoratorArgument(BoundExpression node, Symbol decoratorMember, BindingTimeAnalyzerFlags flags)
        {
            BoundExpression decoratorArgument;
            if (_decoratorArguments.TryGetValue(decoratorMember, out decoratorArgument))
            {
                return Visit(decoratorArgument, flags | BindingTimeAnalyzerFlags.InMetaDecorationArgument);
            }
            else
            {
                // No value was assigned to the field/property manually or in the decorator constructor, so it contains the default value for the type
                TypeSymbol type = node.Type;
                return (type.IsClassType() || MetaUtils.CheckIsSimpleStaticValueType(node.Type, Compilation))
                        ? new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue)
                        : new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }
        }

        private bool CheckIsSpliceLocation(BoundCall call)
        {
            MethodSymbol method = call.Method;
            switch (_targetMemberKind)
            {
                case DecoratedMemberKind.Constructor:
                case DecoratedMemberKind.Method:
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
                            // Disallow calls to MethodBase.Invoke(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this method through a different local variable, leading to infinite recursion)
                            Error(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.Destructor:
                    if (call.Method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__Invoke))
                    {
                        // This is a call to MethodBase.Invoke(object obj, object[] parameters) with null as a second argument (destructors never have arguments)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, _decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[1])
                            && call.Arguments[1].Kind == BoundKind.Literal
                            && ((BoundLiteral)call.Arguments[1]).ConstantValue.IsNull)
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to MethodBase.Invoke(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this method through a different local variable, leading to infinite recursion)
                            Error(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.IndexerGet:
                    if (call.Method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue2))
                    {
                        // This is a call to PropertyInfo.GetValue(object obj, object[] index)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, _decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[1])
                            && (call.Arguments[1].Kind == BoundKind.Parameter || call.Arguments[1].Kind == BoundKind.Local))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.GetValue(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this indexer through a different local variable, leading to infinite recursion)
                            Error(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.IndexerSet:
                    if (call.Method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue2))
                    {
                        // This is a call to PropertyInfo.SetValue(object obj, object value, object[] index)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, _decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[1])
                            && (call.Arguments[2].Kind == BoundKind.Parameter || call.Arguments[2].Kind == BoundKind.Local))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.SetValue(object obj, object value, object[] index) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this indexer through a different local variable, leading to infinite recursion)
                            Error(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.PropertyGet:
                    if (call.Method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue))
                    {
                        // This is a call to PropertyInfo.GetValue(object obj)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, _decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[1]))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.GetValue(object obj) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this property through a different local variable, leading to infinite recursion)
                            Error(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.PropertySet:
                    if (call.Method == Compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue))
                    {
                        // This is a call to PropertyInfo.SetValue(object obj, object value)
                        if (call.ReceiverOpt != null
                            && CheckIsSpecificParameter(call.ReceiverOpt, _decoratorMethod.Parameters[0])
                            && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[1]))
                        {
                            return true;
                        }
                        else
                        {
                            // Disallow calls to PropertyInfo.SetValue(object obj, object value) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this property through a different local variable, leading to infinite recursion)
                            Error(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                default:
                    throw ExceptionUtilities.Unreachable;
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
