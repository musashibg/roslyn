using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using Roslyn.Utilities;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class DecorationBindingTimeAnalyzer : BoundTreeVisitor<DecorationBindingTimeAnalyzerFlags, BindingTimeAnalysisResult>
    {
        private readonly CSharpCompilation _compilation;
        private readonly SourceMemberMethodSymbol _decoratorMethod;
        private readonly List<EncapsulatingStatementKind> _encapsulatingStatements;

        private ImmutableDictionary<Symbol, BindingTime> _variableBindingTimes;
        private bool _hasChangesInVariableBindingTimes;
        private DynamicInterruptionKind _currentDynamicInterruption;

        public ImmutableDictionary<Symbol, BindingTime> VariableBindingTimes
        {
            get { return _variableBindingTimes; }
        }

        public DecorationBindingTimeAnalyzer(
            CSharpCompilation compilation,
            SourceMemberMethodSymbol decoratorMethod)
        {
            _compilation = compilation;
            _decoratorMethod = decoratorMethod;
            _encapsulatingStatements = new List<EncapsulatingStatementKind>();
        }

        public void PerformAnalysis()
        {
            Debug.Assert(_decoratorMethod.DecoratorMethodVariableTypes != null && _decoratorMethod.EarlyBoundBody != null);

            // Build the most optimistic variable binding times configuration, where as many variables as possible have static binding times
            _variableBindingTimes = _decoratorMethod.DecoratorMethodVariableTypes.ToImmutableDictionary(
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
                        return MetaUtils.CheckIsSimpleStaticValueType(kv.Value.OrdinaryType, _compilation)
                                ? BindingTime.StaticSimpleValue
                                : BindingTime.StaticComplexValue;
                    }
                });

            BoundBlock decoratorBody = _decoratorMethod.EarlyBoundBody;

            // Perform the fixpoint iteration process until the variable binding times stabilize
            do
            {
                _hasChangesInVariableBindingTimes = false;
                Visit(decoratorBody, DecorationBindingTimeAnalyzerFlags.None);
            }
            while (_hasChangesInVariableBindingTimes);
        }

        public override BindingTimeAnalysisResult Visit(BoundNode node, DecorationBindingTimeAnalyzerFlags flags)
        {
            if (node == null)
            {
                return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
            }
            else
            {
                if (_currentDynamicInterruption != DynamicInterruptionKind.None)
                {
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode;
                }
                return base.Visit(node, flags);
            }
        }

        public override BindingTimeAnalysisResult VisitAddressOfOperator(BoundAddressOfOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitAnonymousObjectCreationExpression(BoundAnonymousObjectCreationExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> argumentResults = VisitArguments(node.Arguments, default(ImmutableArray<RefKind>), flags, true);
            ImmutableArray<BindingTimeAnalysisResult> declarationResults = VisitList(node.Declarations, flags);
            MakeComplexValuedSymbolsDynamic(declarationResults);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitAnonymousPropertyDeclaration(BoundAnonymousPropertyDeclaration node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitArgList(BoundArgList node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitArgListOperator(BoundArgListOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitArrayAccess(BoundArrayAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult expressionResult = Visit(node.Expression, flags);
            Debug.Assert(expressionResult.BindingTime != BindingTime.StaticSimpleValue);
            ImmutableArray<BindingTimeAnalysisResult> indicesResults = VisitList(node.Indices, flags);
            BindingTimeAnalysisResult indicesCompoundResult = GetCompoundResult(indicesResults);
            Debug.Assert(indicesCompoundResult.BindingTime == BindingTime.StaticSimpleValue || indicesCompoundResult.BindingTime == BindingTime.Dynamic);

            BindingTime bindingTime;
            if (expressionResult.BindingTime != BindingTime.Dynamic && indicesCompoundResult.BindingTime == BindingTime.Dynamic)
            {
                MakeComplexValuedSymbolsDynamic(expressionResult);
                bindingTime = BindingTime.Dynamic;
            }
            else if (expressionResult.BindingTime == BindingTime.StaticArgumentArray)
            {
                bindingTime = BindingTime.Dynamic;
            }
            else if (MetaUtils.CheckIsSimpleStaticValueType(node.Type, _compilation))
            {
                bindingTime = BindingTime.StaticSimpleValue;
            }
            else
            {
                bindingTime = BindingTime.StaticComplexValue;
            }
            return new BindingTimeAnalysisResult(bindingTime, expressionResult.MainSymbol, expressionResult.ComplexValuedSymbols);
        }

        public override BindingTimeAnalysisResult VisitArrayCreation(BoundArrayCreation node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Consider: supporting creation of static-valued arrays
            ImmutableArray<BindingTimeAnalysisResult> boundsResults = VisitList(node.Bounds, flags);
            MakeComplexValuedSymbolsDynamic(boundsResults);
            BindingTimeAnalysisResult initializerResult = Visit(node.InitializerOpt, flags);
            MakeComplexValuedSymbolsDynamic(initializerResult);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitArrayInitialization(BoundArrayInitialization node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Consider: supporting creation of static-valued arrays
            ImmutableArray<BindingTimeAnalysisResult> initializersResults = VisitList(node.Initializers, flags);
            return GetCompoundResult(initializersResults);
        }

        public override BindingTimeAnalysisResult VisitArrayLength(BoundArrayLength node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult expressionResult = Visit(node.Expression, flags);
            return expressionResult.BindingTime == BindingTime.Dynamic
                    ? new BindingTimeAnalysisResult(BindingTime.Dynamic)
                    : new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitAsOperator(BoundAsOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult operandResult = Visit(node.Operand, flags);
            if ((operandResult.BindingTime == BindingTime.StaticSimpleValue && !node.Conversion.IsUserDefined)
                || (operandResult.BindingTime == BindingTime.StaticComplexValue
                    && (node.Conversion.IsBoxing || node.Conversion.Kind == ConversionKind.ImplicitReference)))
            {
                return operandResult;
            }
            else
            {
                MakeComplexValuedSymbolsDynamic(operandResult);
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }
        }

        public override BindingTimeAnalysisResult VisitAssignmentOperator(BoundAssignmentOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult leftResult = Visit(node.Left, flags);
            BindingTimeAnalysisResult rightResult = Visit(node.Right, flags);

            BindingTime bindingTime = leftResult.BindingTime;
            ImmutableHashSet<Symbol> complexValuedSymbols = leftResult.ComplexValuedSymbols;
            switch (bindingTime)
            {
                case BindingTime.StaticSimpleValue:
                case BindingTime.StaticComplexValue:
                    Debug.Assert(rightResult.BindingTime != BindingTime.StaticArgumentArray);
                    if (rightResult.BindingTime == BindingTime.Dynamic || flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode))
                    {
                        MakeMainSymbolDynamic(leftResult);
                        MakeComplexValuedSymbolsDynamic(leftResult, false);
                        bindingTime = BindingTime.Dynamic;
                        complexValuedSymbols = ImmutableHashSet<Symbol>.Empty;
                    }
                    else
                    {
                        complexValuedSymbols = complexValuedSymbols.Union(rightResult.ComplexValuedSymbols);
                    }
                    break;

                case BindingTime.StaticArgumentArray:
                    Debug.Assert(rightResult.BindingTime != BindingTime.StaticSimpleValue && rightResult.BindingTime != BindingTime.StaticComplexValue);
                    if (rightResult.BindingTime == BindingTime.Dynamic || flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode))
                    {
                        MakeMainSymbolDynamic(leftResult);
                        MakeComplexValuedSymbolsDynamic(leftResult, false);
                        bindingTime = BindingTime.Dynamic;
                        complexValuedSymbols = ImmutableHashSet<Symbol>.Empty;
                    }
                    else
                    {
                        complexValuedSymbols = complexValuedSymbols.Union(rightResult.ComplexValuedSymbols);
                    }
                    break;

                default:
                    Debug.Assert(leftResult.BindingTime == BindingTime.Dynamic);
                    if (rightResult.BindingTime != BindingTime.StaticSimpleValue && rightResult.BindingTime != BindingTime.Dynamic)
                    {
                        MakeComplexValuedSymbolsDynamic(rightResult);
                    }
                    break;
            }
            return new BindingTimeAnalysisResult(bindingTime, leftResult.MainSymbol, complexValuedSymbols);
        }

        public override BindingTimeAnalysisResult VisitAttribute(BoundAttribute node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitAwaitExpression(BoundAwaitExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return Visit(node.Expression, flags);
        }

        public override BindingTimeAnalysisResult VisitBadExpression(BoundBadExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitBadStatement(BoundBadStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitBaseReference(BoundBaseReference node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitBinaryOperator(BoundBinaryOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult leftResult = Visit(node.Left, flags);
            BindingTimeAnalysisResult rightResult = Visit(node.Right, flags);

            Debug.Assert(leftResult.BindingTime != BindingTime.StaticArgumentArray && rightResult.BindingTime != BindingTime.StaticArgumentArray);

            return GetCompoundResult(leftResult, rightResult);
        }

        public override BindingTimeAnalysisResult VisitBlock(BoundBlock node, DecorationBindingTimeAnalyzerFlags flags)
        {
            VisitList(node.Statements, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitBreakStatement(BoundBreakStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            if (flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode))
            {
                Debug.Assert(_encapsulatingStatements.Any(es => es.Statement() == EncapsulatingStatementKind.Switch || es.Statement() == EncapsulatingStatementKind.Loop));
                bool isDynamicallyControledStatement = true;
                for (int i = _encapsulatingStatements.Count - 1; i >= 0; i--)
                {
                    EncapsulatingStatementKind encapsulatingStatementKind = _encapsulatingStatements[i];
                    if (encapsulatingStatementKind.Statement() == EncapsulatingStatementKind.Switch || encapsulatingStatementKind.Statement() == EncapsulatingStatementKind.Loop)
                    {
                        isDynamicallyControledStatement = encapsulatingStatementKind.IsDynamic();
                        break;
                    }
                }
                // If the break statement appears in dynamically reachable code, but the switch or loop it belongs to is not dynamically controlled, we treat all subsequent statements
                // until the end of the encapsulating switch or loop as dynamically reachable
                if (!isDynamicallyControledStatement)
                {
                    _currentDynamicInterruption |= DynamicInterruptionKind.Break;
                }
            }
            return null;
        }

        public override BindingTimeAnalysisResult VisitCall(BoundCall node, DecorationBindingTimeAnalyzerFlags flags)
        {
            if (CheckIsSpliceLocation(node) || CheckIsBaseDecoratorMethodCall(node))
            {
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }

            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray);

            MethodSymbol method = node.Method;
            bool forceDynamic = true;
            if (receiverResult.BindingTime != BindingTime.Dynamic)
            {
                // Detect well-known method invocations
                if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAssignableFrom)
                    || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__GetParameters)
                    || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T)
                    || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2)
                    || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__CloneArguments)
                    || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__CloneArgumentsToObjectArray)
                    || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType)
                    || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType))
                {
                    forceDynamic = false;
                }
            }

            if (forceDynamic)
            {
                MakeComplexValuedSymbolsDynamic(receiverResult);
            }

            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, forceDynamic);

            if (!forceDynamic)
            {
                // Handle well-known method invocations with static binding time
                if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAssignableFrom))
                {
                    if (argumentsResults[0].BindingTime != BindingTime.Dynamic)
                    {
                        return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
                    }
                }
                else if (method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T)
                         || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2))
                {
                    if (argumentsResults[0].BindingTime != BindingTime.Dynamic)
                    {
                        // We will replace the method call with an instantiation of the attribute, if any; the receiver's complex symbols do not need to be propagated to this expression
                        return new BindingTimeAnalysisResult(BindingTime.StaticComplexValue);
                    }
                }
                else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__GetParameters))
                {
                    return new BindingTimeAnalysisResult(BindingTime.StaticComplexValue, null, receiverResult.ComplexValuedSymbols);
                }
                else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__CloneArguments))
                {
                    return new BindingTimeAnalysisResult(BindingTime.StaticArgumentArray);
                }
                else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType))
                {
                    if (argumentsResults[0].BindingTime != BindingTime.Dynamic
                        && argumentsResults[1].BindingTime != BindingTime.Dynamic)
                    {
                        return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
                    }
                    else
                    {
                        MakeComplexValuedSymbolsDynamic(argumentsResults);
                    }
                }
                else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType))
                {
                    if (argumentsResults[0].BindingTime != BindingTime.Dynamic)
                    {
                        return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
                    }
                }
            }

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitCatchBlock(BoundCatchBlock node, DecorationBindingTimeAnalyzerFlags flags)
        {
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.CatchBlock | EncapsulatingStatementKind.DynamicallyControlled);

            BindingTimeAnalysisResult exceptionSourceResult = Visit(node.ExceptionSourceOpt, flags);
            MakeComplexValuedSymbolsDynamic(exceptionSourceResult);

            BindingTimeAnalysisResult exceptionFilterResult = Visit(node.ExceptionFilterOpt, flags);
            Debug.Assert(exceptionFilterResult.BindingTime == BindingTime.Dynamic || exceptionFilterResult.BindingTime == BindingTime.StaticSimpleValue);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            Visit(node.Body, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitCollectionElementInitializer(BoundCollectionElementInitializer node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitList(node.Arguments, flags);
            return GetCompoundResult(argumentsResults);
        }

        public override BindingTimeAnalysisResult VisitCollectionInitializerExpression(BoundCollectionInitializerExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> initializersResults = VisitList(node.Initializers, flags);
            return GetCompoundResult(initializersResults);
        }

        public override BindingTimeAnalysisResult VisitComplexConditionalReceiver(BoundComplexConditionalReceiver node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult valueTypeReceiverResult = Visit(node.ValueTypeReceiver, flags | DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode);
            MakeComplexValuedSymbolsDynamic(valueTypeReceiverResult);

            BindingTimeAnalysisResult referenceTypeReceiverResult = Visit(node.ReferenceTypeReceiver, flags | DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode);
            MakeComplexValuedSymbolsDynamic(referenceTypeReceiverResult);

            Debug.Assert(valueTypeReceiverResult.BindingTime != BindingTime.StaticArgumentArray && referenceTypeReceiverResult.BindingTime != BindingTime.StaticArgumentArray);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult leftResult = Visit(node.Left, flags);
            BindingTimeAnalysisResult rightResult = Visit(node.Right, flags);

            Debug.Assert(leftResult.BindingTime != BindingTime.StaticArgumentArray && rightResult.BindingTime != BindingTime.StaticArgumentArray);

            BindingTime bindingTime = leftResult.BindingTime;
            ImmutableHashSet<Symbol> complexValuedSymbols = leftResult.ComplexValuedSymbols;
            if (bindingTime == BindingTime.StaticSimpleValue || bindingTime == BindingTime.StaticComplexValue)
            {
                if (rightResult.BindingTime == BindingTime.Dynamic || flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode))
                {
                    MakeMainSymbolDynamic(leftResult);
                    MakeComplexValuedSymbolsDynamic(leftResult, false);
                    bindingTime = BindingTime.Dynamic;
                    complexValuedSymbols = ImmutableHashSet<Symbol>.Empty;
                }
                else
                {
                    complexValuedSymbols = leftResult.ComplexValuedSymbols.Union(rightResult.ComplexValuedSymbols);
                }
            }
            return new BindingTimeAnalysisResult(bindingTime, leftResult.MainSymbol, complexValuedSymbols);
        }

        public override BindingTimeAnalysisResult VisitConditionalAccess(BoundConditionalAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.Receiver, flags);
            BindingTimeAnalysisResult accessExpressionResult = Visit(node.AccessExpression, flags);
            return GetCompoundResult(receiverResult, accessExpressionResult);
        }

        public override BindingTimeAnalysisResult VisitConditionalGoto(BoundConditionalGoto node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Consider: Only force entirely dynamic decorator method body if the code contains dynamically reachable jumps

            BindingTimeAnalysisResult conditionResult = Visit(node.Condition, flags);
            // If the code contains a jump statement, we want to make all symbols except the decorator method's parameters dynamic
            foreach (Symbol symbol in VariableBindingTimes.Keys)
            {
                if (symbol.Kind != SymbolKind.Parameter || !_decoratorMethod.Parameters.Contains((ParameterSymbol)symbol))
                {
                    MakeSymbolDynamic(symbol);
                }
            }
            return null;
        }

        public override BindingTimeAnalysisResult VisitConditionalOperator(BoundConditionalOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult conditionResult = Visit(node.Condition, flags);
            if (conditionResult.BindingTime != BindingTime.StaticSimpleValue)
            {
                flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode;
            }

            BindingTimeAnalysisResult consequenceResult = Visit(node.Consequence, flags);
            BindingTimeAnalysisResult alternativeResult = Visit(node.Alternative, flags);

            Debug.Assert(conditionResult.BindingTime != BindingTime.StaticArgumentArray
                         && consequenceResult.BindingTime != BindingTime.StaticArgumentArray
                         && alternativeResult.BindingTime != BindingTime.StaticArgumentArray);

            return GetCompoundResult(conditionResult, consequenceResult, alternativeResult);
        }

        public override BindingTimeAnalysisResult VisitConditionalReceiver(BoundConditionalReceiver node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitContinueStatement(BoundContinueStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            if (flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode))
            {
                Debug.Assert(_encapsulatingStatements.Any(es => es.Statement() == EncapsulatingStatementKind.Loop));
                bool isDynamicallyControlledLoop = true;
                for (int i = _encapsulatingStatements.Count - 1; i >= 0; i--)
                {
                    EncapsulatingStatementKind encapsulatingStatementKind = _encapsulatingStatements[i];
                    if (encapsulatingStatementKind.Statement() == EncapsulatingStatementKind.Loop)
                    {
                        isDynamicallyControlledLoop = encapsulatingStatementKind.IsDynamic();
                        break;
                    }
                }
                // If the continue statement appears in dynamically accessible code, but the loop it belongs to is not dynamically controlled, we treat all subsequent statements
                // until the end of the encapsulating loop as dynamically reachable
                if (!isDynamicallyControlledLoop)
                {
                    _currentDynamicInterruption |= DynamicInterruptionKind.Continue;
                }
            }
            return null;
        }

        public override BindingTimeAnalysisResult VisitConversion(BoundConversion node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult operandResult = Visit(node.Operand, flags);
            if ((operandResult.BindingTime == BindingTime.StaticSimpleValue && !node.ConversionKind.IsUserDefinedConversion())
                || (operandResult.BindingTime == BindingTime.StaticComplexValue
                    && (node.ConversionKind == ConversionKind.Boxing || node.ConversionKind == ConversionKind.ImplicitReference)))
            {
                return operandResult;
            }
            else
            {
                MakeComplexValuedSymbolsDynamic(operandResult);
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }
        }

        public override BindingTimeAnalysisResult VisitDecorator(BoundDecorator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitDefaultOperator(BoundDefaultOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitDelegateCreationExpression(BoundDelegateCreationExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult argumentResult = Visit(node.Argument, flags);
            MakeComplexValuedSymbolsDynamic(argumentResult);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitDoStatement(BoundDoStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            DynamicInterruptionKind oldDynamicInterruptionKind = _currentDynamicInterruption;
            _currentDynamicInterruption = DynamicInterruptionKind.None;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);

            bool traversedSuccessfully = false;
            do
            {
                BindingTimeAnalysisResult conditionResult = Visit(node.Condition, flags);
                if (conditionResult.BindingTime != BindingTime.StaticSimpleValue)
                {
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                Visit(node.Body, flags);

                if (!flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop) && _currentDynamicInterruption != DynamicInterruptionKind.None)
                {
                    // If there is a dynamically-reachable break or continue statement, we make the entire loop dynamic and repeat the traversal
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                else
                {
                    traversedSuccessfully = true;
                }
            }
            while (!traversedSuccessfully);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);
            _currentDynamicInterruption = oldDynamicInterruptionKind;

            return null;
        }

        public override BindingTimeAnalysisResult VisitDup(BoundDup node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitDynamicCollectionElementInitializer(BoundDynamicCollectionElementInitializer node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitList(node.Arguments, flags);
            return GetCompoundResult(argumentsResults);
        }

        public override BindingTimeAnalysisResult VisitDynamicIndexerAccess(BoundDynamicIndexerAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray);
            MakeComplexValuedSymbolsDynamic(receiverResult);

            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, true);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitDynamicInvocation(BoundDynamicInvocation node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult expressionResult = Visit(node.Expression, flags);
            Debug.Assert(expressionResult.BindingTime != BindingTime.StaticArgumentArray);
            MakeComplexValuedSymbolsDynamic(expressionResult);

            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, true);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitDynamicMemberAccess(BoundDynamicMemberAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.Receiver, flags);
            Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray);
            MakeComplexValuedSymbolsDynamic(receiverResult);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitDynamicObjectCreationExpression(BoundDynamicObjectCreationExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, true);

            BindingTimeAnalysisResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, flags);
            MakeComplexValuedSymbolsDynamic(initializerExpressionResult);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitDynamicObjectInitializerMember(BoundDynamicObjectInitializerMember node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitEventAccess(BoundEventAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray);
            MakeComplexValuedSymbolsDynamic(receiverResult);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitEventAssignmentOperator(BoundEventAssignmentOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            MakeMainSymbolDynamic(receiverResult);
            MakeComplexValuedSymbolsDynamic(receiverResult);

            BindingTimeAnalysisResult argumentResult = Visit(node.Argument, flags);
            MakeComplexValuedSymbolsDynamic(argumentResult);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitExpressionStatement(BoundExpressionStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            Visit(node.Expression, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitFieldAccess(BoundFieldAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            MakeComplexValuedSymbolsDynamic(receiverResult);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitFieldEqualsValue(BoundFieldEqualsValue node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return Visit(node.Value, flags);
        }

        public override BindingTimeAnalysisResult VisitFieldInfo(BoundFieldInfo node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitFieldInitializer(BoundFieldInitializer node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitFixedLocalCollectionInitializer(BoundFixedLocalCollectionInitializer node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult expressionResult = Visit(node.Expression, flags);
            MakeComplexValuedSymbolsDynamic(expressionResult);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitFixedStatement(BoundFixedStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            Visit(node.Declarations, flags);
            Visit(node.Body, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitForEachStatement(BoundForEachStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            DynamicInterruptionKind oldDynamicInterruptionKind = _currentDynamicInterruption;
            _currentDynamicInterruption = DynamicInterruptionKind.None;
            LocalSymbol iterationVariable = node.IterationVariable;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);

            bool traversedSuccessfully = false;
            do
            {
                BindingTimeAnalysisResult expressionResult = Visit(node.Expression, flags);
                if (expressionResult.BindingTime == BindingTime.Dynamic)
                {
                    MakeSymbolDynamic(iterationVariable);
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                Visit(node.Body, flags);

                if (!flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop) && _currentDynamicInterruption != DynamicInterruptionKind.None)
                {
                    // If there is a dynamically-reachable break or continue statement, we make the entire loop dynamic and repeat the traversal
                    MakeSymbolDynamic(iterationVariable);
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                else
                {
                    traversedSuccessfully = true;
                }
            }
            while (!traversedSuccessfully);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);
            _currentDynamicInterruption = oldDynamicInterruptionKind;

            return null;
        }

        public override BindingTimeAnalysisResult VisitForStatement(BoundForStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            DynamicInterruptionKind oldDynamicInterruptionKind = _currentDynamicInterruption;
            _currentDynamicInterruption = DynamicInterruptionKind.None;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);

            bool traversedSuccessfully = false;
            do
            {
                Visit(node.Initializer, flags);
                BindingTimeAnalysisResult conditionResult = Visit(node.Condition, flags);
                if (conditionResult.BindingTime != BindingTime.StaticSimpleValue)
                {
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                Visit(node.Increment, flags);
                Visit(node.Body, flags);

                if (!flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop) && _currentDynamicInterruption != DynamicInterruptionKind.None)
                {
                    // If there is a dynamically-reachable break or continue statement, we make the entire loop dynamic and repeat the traversal
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                else
                {
                    traversedSuccessfully = true;
                }
            }
            while (!traversedSuccessfully);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);
            _currentDynamicInterruption = oldDynamicInterruptionKind;

            return null;
        }

        public override BindingTimeAnalysisResult VisitGlobalStatementInitializer(BoundGlobalStatementInitializer node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitGotoStatement(BoundGotoStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Consider: Only force entirely dynamic decorator method body if the code contains dynamically reachable jumps

            // If the code contains a jump statement, we want to make all symbols except the decorator method's parameters dynamic
            foreach (Symbol symbol in VariableBindingTimes.Keys)
            {
                if (symbol.Kind != SymbolKind.Parameter || !_decoratorMethod.Parameters.Contains((ParameterSymbol)symbol))
                {
                    MakeSymbolDynamic(symbol);
                }
            }
            return null;
        }

        public override BindingTimeAnalysisResult VisitHoistedFieldAccess(BoundHoistedFieldAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitHostObjectMemberReference(BoundHostObjectMemberReference node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should not exist in non-script code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitIfStatement(BoundIfStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            BindingTimeAnalysisResult conditionResult = Visit(node.Condition, flags);
            if (conditionResult.BindingTime == BindingTime.StaticSimpleValue)
            {
                _encapsulatingStatements.Add(EncapsulatingStatementKind.Conditional);
            }
            else
            {
                flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode;
                _encapsulatingStatements.Add(EncapsulatingStatementKind.Conditional | EncapsulatingStatementKind.DynamicallyControlled);
            }
            Visit(node.Consequence, flags);
            Visit(node.AlternativeOpt, flags);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            return null;
        }

        public override BindingTimeAnalysisResult VisitImplicitReceiver(BoundImplicitReceiver node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitIncrementOperator(BoundIncrementOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult operandResult = Visit(node.Operand, flags);
            Debug.Assert(operandResult.BindingTime != BindingTime.StaticArgumentArray);
            if (operandResult.BindingTime != BindingTime.Dynamic && flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode))
            {
                MakeMainSymbolDynamic(operandResult);
                MakeComplexValuedSymbolsDynamic(operandResult);
                return new BindingTimeAnalysisResult(BindingTime.Dynamic, operandResult.MainSymbol, ImmutableHashSet<Symbol>.Empty);
            }
            else
            {
                return operandResult;
            }
        }

        public override BindingTimeAnalysisResult VisitIndexerAccess(BoundIndexerAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray);
            MakeComplexValuedSymbolsDynamic(receiverResult);

            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, true);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitInterpolatedString(BoundInterpolatedString node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> partsResults = VisitList(node.Parts, flags);
            MakeComplexValuedSymbolsDynamic(partsResults);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitIsOperator(BoundIsOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult operandResult = Visit(node.Operand, flags);
            if (operandResult.BindingTime == BindingTime.Dynamic)
            {
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }
            else
            {
                return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
            }
        }

        public override BindingTimeAnalysisResult VisitLabel(BoundLabel node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitLabelStatement(BoundLabelStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return null;
        }

        public override BindingTimeAnalysisResult VisitLabeledStatement(BoundLabeledStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            Visit(node.Body, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitLambda(BoundLambda node, DecorationBindingTimeAnalyzerFlags flags)
        {
            foreach (ParameterSymbol parameter in node.Symbol.Parameters)
            {
                MakeSymbolDynamic(parameter);
            }
            Visit(node.Body, flags | DecorationBindingTimeAnalyzerFlags.InNestedLambdaBody);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitLiteral(BoundLiteral node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitLocal(BoundLocal node, DecorationBindingTimeAnalyzerFlags flags)
        {
            LocalSymbol localSymbol = node.LocalSymbol;
            BindingTime bindingTime = VariableBindingTimes[localSymbol];
            if (bindingTime != BindingTime.Dynamic && flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InNestedLambdaBody))
            {
                // Any local variables accessed inside a nested lambda function must be designated as dynamic
                MakeSymbolDynamic(localSymbol);
                bindingTime = BindingTime.Dynamic;
            }

            if (bindingTime != BindingTime.Dynamic && bindingTime != BindingTime.StaticSimpleValue)
            {
                return new BindingTimeAnalysisResult(bindingTime, localSymbol, ImmutableHashSet.Create<Symbol>(localSymbol));
            }
            else
            {
                return new BindingTimeAnalysisResult(bindingTime, localSymbol, ImmutableHashSet<Symbol>.Empty);
            }
        }

        public override BindingTimeAnalysisResult VisitLocalDeclaration(BoundLocalDeclaration node, DecorationBindingTimeAnalyzerFlags flags)
        {
            LocalSymbol localSymbol = node.LocalSymbol;
            BindingTime localBindingTime = VariableBindingTimes[localSymbol];
            if (localBindingTime != BindingTime.Dynamic && flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InNestedLambdaBody))
            {
                // Any local variables declared inside a nested lambda function must be designated as dynamic
                MakeSymbolDynamic(localSymbol);
                localBindingTime = BindingTime.Dynamic;
            }

            BindingTimeAnalysisResult initializerResult = Visit(node.InitializerOpt, flags);

            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitList(node.ArgumentsOpt, flags);

            if (localBindingTime != BindingTime.Dynamic
                && (initializerResult.BindingTime == BindingTime.Dynamic
                    || GetCompoundResult(argumentsResults).BindingTime == BindingTime.Dynamic
                    || flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode)))
            {
                MakeSymbolDynamic(localSymbol);
            }
            return null;
        }

        public override BindingTimeAnalysisResult VisitLockStatement(BoundLockStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult argumentResult = Visit(node.Argument, flags);
            MakeMainSymbolDynamic(argumentResult);
            MakeComplexValuedSymbolsDynamic(argumentResult);

            Visit(node.Body, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitLoweredConditionalAccess(BoundLoweredConditionalAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.Receiver, flags);
            if (receiverResult.BindingTime == BindingTime.Dynamic)
            {
                flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode;
            }

            BindingTimeAnalysisResult whenNotNullResult = Visit(node.WhenNotNull, flags);
            BindingTimeAnalysisResult whenNullResult = Visit(node.WhenNullOpt, flags);

            Debug.Assert(receiverResult.BindingTime != BindingTime.StaticArgumentArray
                         && whenNotNullResult.BindingTime != BindingTime.StaticArgumentArray
                         && whenNullResult.BindingTime != BindingTime.StaticArgumentArray);

            return GetCompoundResult(receiverResult, whenNotNullResult, whenNullResult);
        }

        public override BindingTimeAnalysisResult VisitMakeRefOperator(BoundMakeRefOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitMethodGroup(BoundMethodGroup node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            MakeComplexValuedSymbolsDynamic(receiverResult);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitMethodInfo(BoundMethodInfo node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitMultipleLocalDeclarations(BoundMultipleLocalDeclarations node, DecorationBindingTimeAnalyzerFlags flags)
        {
            VisitList(node.LocalDeclarations, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitNameOfOperator(BoundNameOfOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitNamespaceExpression(BoundNamespaceExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitNewT(BoundNewT node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, flags);
            MakeComplexValuedSymbolsDynamic(initializerExpressionResult);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitNoOpStatement(BoundNoOpStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return null;
        }

        public override BindingTimeAnalysisResult VisitNoPiaObjectCreationExpression(BoundNoPiaObjectCreationExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, flags);
            MakeComplexValuedSymbolsDynamic(initializerExpressionResult);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitNullCoalescingOperator(BoundNullCoalescingOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult leftOperandResult = Visit(node.LeftOperand, flags);
            if (leftOperandResult.BindingTime == BindingTime.Dynamic)
            {
                flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode;
            }
            BindingTimeAnalysisResult rightOperandResult = Visit(node.RightOperand, flags);

            Debug.Assert(leftOperandResult.BindingTime != BindingTime.StaticArgumentArray && rightOperandResult.BindingTime != BindingTime.StaticArgumentArray);

            return GetCompoundResult(leftOperandResult, rightOperandResult);
        }

        public override BindingTimeAnalysisResult VisitObjectCreationExpression(BoundObjectCreationExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, true);

            BindingTimeAnalysisResult initializerExpressionResult = Visit(node.InitializerExpressionOpt, flags);
            MakeComplexValuedSymbolsDynamic(initializerExpressionResult);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitObjectInitializerExpression(BoundObjectInitializerExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> initializersResults = VisitList(node.Initializers, flags);
            return GetCompoundResult(initializersResults);
        }

        public override BindingTimeAnalysisResult VisitObjectInitializerMember(BoundObjectInitializerMember node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ImmutableArray<BindingTimeAnalysisResult> argumentsResults = VisitArguments(node.Arguments, node.ArgumentRefKindsOpt, flags, false);
            return GetCompoundResult(argumentsResults);
        }

        public override BindingTimeAnalysisResult VisitParameter(BoundParameter node, DecorationBindingTimeAnalyzerFlags flags)
        {
            ParameterSymbol parameterSymbol = node.ParameterSymbol;
            BindingTime bindingTime = VariableBindingTimes[parameterSymbol];
            if (bindingTime != BindingTime.Dynamic && bindingTime != BindingTime.StaticSimpleValue)
            {
                return new BindingTimeAnalysisResult(bindingTime, parameterSymbol, ImmutableHashSet.Create<Symbol>(parameterSymbol));
            }
            else
            {
                return new BindingTimeAnalysisResult(bindingTime, parameterSymbol, ImmutableHashSet<Symbol>.Empty);
            }
        }

        public override BindingTimeAnalysisResult VisitParameterEqualsValue(BoundParameterEqualsValue node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return Visit(node.Value, flags);
        }

        public override BindingTimeAnalysisResult VisitPointerElementAccess(BoundPointerElementAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitPointerIndirectionOperator(BoundPointerIndirectionOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitPreviousSubmissionReference(BoundPreviousSubmissionReference node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should not exist in non-script code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitPropertyAccess(BoundPropertyAccess node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BoundExpression receiverOpt = node.ReceiverOpt;
            BindingTimeAnalysisResult receiverResult = Visit(receiverOpt, flags);

            if (receiverResult.BindingTime != BindingTime.Dynamic)
            {
                // Handle well-know property accesses with static binding time
                PropertySymbol property = node.PropertySymbol;
                if (receiverOpt != null)
                {
                    if (receiverOpt.Type.IsArray())
                    {
                        if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Array__Length))
                        {
                            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
                        }
                    }
                    if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Type))
                    {
                        if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Name)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace))
                        {
                            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
                        }
                    }
                    else if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo))
                    {
                        if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                        {
                            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
                        }
                    }
                    else if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo))
                    {
                        if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType)
                            || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position))
                        {
                            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
                        }
                        else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member))
                        {
                            return new BindingTimeAnalysisResult(BindingTime.StaticComplexValue);
                        }
                    }
                }

                MakeComplexValuedSymbolsDynamic(receiverResult);
            }

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitPropertyEqualsValue(BoundPropertyEqualsValue node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return Visit(node.Value, flags);
        }

        public override BindingTimeAnalysisResult VisitPropertyGroup(BoundPropertyGroup node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult receiverResult = Visit(node.ReceiverOpt, flags);
            MakeComplexValuedSymbolsDynamic(receiverResult);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitPseudoVariable(BoundPseudoVariable node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitQueryClause(BoundQueryClause node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return Visit(node.Value, flags);
        }

        public override BindingTimeAnalysisResult VisitRangeVariable(BoundRangeVariable node, DecorationBindingTimeAnalyzerFlags flags)
        {
            RangeVariableSymbol rangeVariableSymbol = node.RangeVariableSymbol;
            Debug.Assert(VariableBindingTimes[rangeVariableSymbol] == BindingTime.Dynamic);

            BindingTimeAnalysisResult valueResult = Visit(node.Value, flags);
            MakeComplexValuedSymbolsDynamic(valueResult);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic, rangeVariableSymbol, ImmutableHashSet<Symbol>.Empty);
        }

        public override BindingTimeAnalysisResult VisitRefTypeOperator(BoundRefTypeOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitRefValueOperator(BoundRefValueOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitReturnStatement(BoundReturnStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            Visit(node.ExpressionOpt, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitSequence(BoundSequence node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitSequencePoint(BoundSequencePoint node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitSequencePointExpression(BoundSequencePointExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return Visit(node.Expression, flags);
        }

        public override BindingTimeAnalysisResult VisitSequencePointWithSpan(BoundSequencePointWithSpan node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitSizeOfOperator(BoundSizeOfOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitStackAllocArrayCreation(BoundStackAllocArrayCreation node, DecorationBindingTimeAnalyzerFlags flags)
        {
            Visit(node.Count, flags);
            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitStateMachineScope(BoundStateMachineScope node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitStatementList(BoundStatementList node, DecorationBindingTimeAnalyzerFlags flags)
        {
            VisitList(node.Statements, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitStringInsert(BoundStringInsert node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult valueResult = Visit(node.Value, flags);
            MakeComplexValuedSymbolsDynamic(valueResult);

            BindingTimeAnalysisResult alignmentResult = Visit(node.Alignment, flags);
            MakeComplexValuedSymbolsDynamic(alignmentResult);

            BindingTimeAnalysisResult formatResult = Visit(node.Format, flags);
            MakeComplexValuedSymbolsDynamic(formatResult);

            Debug.Assert(valueResult.BindingTime != BindingTime.StaticArgumentArray
                         && alignmentResult.BindingTime != BindingTime.StaticArgumentArray
                         && formatResult.BindingTime != BindingTime.StaticArgumentArray);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitSwitchLabel(BoundSwitchLabel node, DecorationBindingTimeAnalyzerFlags flags)
        {
            Visit(node.ExpressionOpt, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitSwitchSection(BoundSwitchSection node, DecorationBindingTimeAnalyzerFlags flags)
        {
            VisitList(node.BoundSwitchLabels, flags);
            VisitList(node.Statements, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitSwitchStatement(BoundSwitchStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            DynamicInterruptionKind oldDynamicInterruptionKind = _currentDynamicInterruption;
            _currentDynamicInterruption = DynamicInterruptionKind.None;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            BindingTimeAnalysisResult expressionResult = Visit(node.BoundExpression, flags);
            if (expressionResult.BindingTime == BindingTime.StaticSimpleValue)
            {
                _encapsulatingStatements.Add(EncapsulatingStatementKind.Switch);
            }
            else
            {
                flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode;
                _encapsulatingStatements.Add(EncapsulatingStatementKind.Switch | EncapsulatingStatementKind.DynamicallyControlled);
            }
            VisitList(node.SwitchSections, flags);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);
            // Break interruptions are captured by the switch statement, but continue interruptions pass through to the encapsulating loop
            _currentDynamicInterruption = oldDynamicInterruptionKind | (_currentDynamicInterruption & ~DynamicInterruptionKind.Break);

            return null;
        }

        public override BindingTimeAnalysisResult VisitThisReference(BoundThisReference node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitThrowStatement(BoundThrowStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            Visit(node.ExpressionOpt, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitTryStatement(BoundTryStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.TryBlock | EncapsulatingStatementKind.DynamicallyControlled);

            // The execution of a try block can be terminated prematurely, sending control flow to a catch or finally block; any updates to variables within it cannot be statically evaluated
            Visit(node.TryBlock, flags | DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            // Whether a catch block will be entered or not cannot be resolved statically, so any updates to variables within it cannot be statically evaluated
            VisitList(node.CatchBlocks, flags | DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode);

            _encapsulatingStatements.Add(EncapsulatingStatementKind.FinallyBlock);

            // A finally block is always executed, so updates to static variables within it can be statically evaluated
            Visit(node.FinallyBlockOpt, flags);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);

            return null;
        }

        public override BindingTimeAnalysisResult VisitTypeExpression(BoundTypeExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitTypeOfOperator(BoundTypeOfOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            return new BindingTimeAnalysisResult(BindingTime.StaticSimpleValue);
        }

        public override BindingTimeAnalysisResult VisitTypeOrInstanceInitializers(BoundTypeOrInstanceInitializers node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitTypeOrValueExpression(BoundTypeOrValueExpression node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult typeExpressionResult = Visit(node.Data.TypeExpression, flags | DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode);
            BindingTimeAnalysisResult valueExpressionResult = Visit(node.Data.ValueExpression, flags | DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode);

            Debug.Assert(typeExpressionResult.BindingTime != BindingTime.StaticArgumentArray && valueExpressionResult.BindingTime != BindingTime.StaticArgumentArray);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitUnaryOperator(BoundUnaryOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult operandResult = Visit(node.Operand, flags);
            Debug.Assert(operandResult.BindingTime != BindingTime.StaticArgumentArray);
            return operandResult;
        }

        public override BindingTimeAnalysisResult VisitUnboundLambda(UnboundLambda node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // The decorator method's body should not contain unbound lambdas
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitUserDefinedConditionalLogicalOperator(BoundUserDefinedConditionalLogicalOperator node, DecorationBindingTimeAnalyzerFlags flags)
        {
            flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyReachableCode;

            BindingTimeAnalysisResult leftResult = Visit(node.Left, flags);
            MakeComplexValuedSymbolsDynamic(leftResult);

            BindingTimeAnalysisResult rightResult = Visit(node.Right, flags);
            MakeComplexValuedSymbolsDynamic(rightResult);

            Debug.Assert(leftResult.BindingTime != BindingTime.StaticArgumentArray && rightResult.BindingTime != BindingTime.StaticArgumentArray);

            return new BindingTimeAnalysisResult(BindingTime.Dynamic);
        }

        public override BindingTimeAnalysisResult VisitUsingStatement(BoundUsingStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            BindingTimeAnalysisResult declarationsResult = Visit(node.DeclarationsOpt, flags);
            MakeComplexValuedSymbolsDynamic(declarationsResult);

            BindingTimeAnalysisResult expressionResult = Visit(node.ExpressionOpt, flags);
            MakeMainSymbolDynamic(expressionResult);
            MakeComplexValuedSymbolsDynamic(expressionResult);

            Visit(node.Body, flags);
            return null;
        }

        public override BindingTimeAnalysisResult VisitWhileStatement(BoundWhileStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            DynamicInterruptionKind oldDynamicInterruptionKind = _currentDynamicInterruption;
            _currentDynamicInterruption = DynamicInterruptionKind.None;
            int encapsulatingStatementIndex = _encapsulatingStatements.Count;
            _encapsulatingStatements.Add(EncapsulatingStatementKind.Loop);

            bool traversedSuccessfully = false;
            do
            {
                BindingTimeAnalysisResult conditionResult = Visit(node.Condition, flags);
                if (conditionResult.BindingTime != BindingTime.StaticSimpleValue)
                {
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                Visit(node.Body, flags);

                if (!flags.HasFlag(DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop) && _currentDynamicInterruption != DynamicInterruptionKind.None)
                {
                    // If there is a dynamically-reachable break or continue statement, we make the entire loop dynamic and repeat the traversal
                    flags |= DecorationBindingTimeAnalyzerFlags.InDynamicallyControlledLoop;
                    _encapsulatingStatements[encapsulatingStatementIndex] = EncapsulatingStatementKind.Loop | EncapsulatingStatementKind.DynamicallyControlled;
                }
                else
                {
                    traversedSuccessfully = true;
                }
            }
            while (!traversedSuccessfully);

            _encapsulatingStatements.RemoveAt(encapsulatingStatementIndex);
            Debug.Assert(_encapsulatingStatements.Count == encapsulatingStatementIndex);
            _currentDynamicInterruption = oldDynamicInterruptionKind;

            return null;
        }

        public override BindingTimeAnalysisResult VisitYieldBreakStatement(BoundYieldBreakStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public override BindingTimeAnalysisResult VisitYieldReturnStatement(BoundYieldReturnStatement node, DecorationBindingTimeAnalyzerFlags flags)
        {
            // Such nodes should already have been rejected by the DecoratorTypeChecker
            throw ExceptionUtilities.Unreachable;
        }

        public ImmutableArray<BindingTimeAnalysisResult> VisitList<T>(ImmutableArray<T> list, DecorationBindingTimeAnalyzerFlags flags)
            where T : BoundNode
        {
            if (list.IsDefaultOrEmpty)
            {
                return ImmutableArray<BindingTimeAnalysisResult>.Empty;
            }
            else
            {
                var itemResults = new BindingTimeAnalysisResult[list.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    itemResults[i] = Visit(list[i], flags);
                }
                return itemResults.ToImmutableArray();
            }
        }

        public ImmutableArray<BindingTimeAnalysisResult> VisitArguments(
            ImmutableArray<BoundExpression> arguments,
            ImmutableArray<RefKind> argumentRefKindsOpt,
            DecorationBindingTimeAnalyzerFlags flags,
            bool forceDynamic)
        {
            if (arguments.IsDefaultOrEmpty)
            {
                return ImmutableArray<BindingTimeAnalysisResult>.Empty;
            }
            else
            {
                ImmutableArray<BindingTimeAnalysisResult> argumentResults = VisitList(arguments, flags);
                for (int i = 0; i < arguments.Length; i++)
                {
                    BindingTimeAnalysisResult argumentResult = argumentResults[i];
                    BindingTime bindingTime = argumentResult.BindingTime;
                    if (bindingTime == BindingTime.Dynamic)
                    {
                        continue;
                    }
                    if ((argumentRefKindsOpt.IsDefaultOrEmpty || argumentRefKindsOpt[i] == RefKind.None)
                        && MetaUtils.CheckIsSimpleStaticValueType(arguments[i].Type, _compilation))
                    {
                        // Simple static values passed by value are safe from modifications and can remain static
                        continue;
                    }
                    if (bindingTime == BindingTime.StaticArgumentArray)
                    {
                        // The DecoratorTypeChecker has allowed the argument array to be passed as an argument, so it is safe from modifications and can remain static
                        continue;
                    }
                    if (!forceDynamic && bindingTime == BindingTime.StaticComplexValue)
                    {
                        // The context in which the argument is passed guarantees that it will remain unmodified
                        continue;
                    }

                    MakeMainSymbolDynamic(argumentResult);
                    MakeComplexValuedSymbolsDynamic(argumentResult);
                    argumentResults = argumentResults.SetItem(i, new BindingTimeAnalysisResult(BindingTime.Dynamic, argumentResult.MainSymbol, argumentResult.ComplexValuedSymbols));
                }
                return argumentResults;
            }
        }

        private void MakeComplexValuedSymbolsDynamic(ImmutableArray<BindingTimeAnalysisResult> results)
        {
            foreach (BindingTimeAnalysisResult result in results)
            {
                MakeComplexValuedSymbolsDynamic(result);
            }
        }

        private void MakeComplexValuedSymbolsDynamic(BindingTimeAnalysisResult result, bool ignoreSimpleValueResults = true)
        {
            if (result.BindingTime == BindingTime.Dynamic)
            {
                // If the expression is dynamically-valued, all of its complex-valued associated symbols should already have been updated to be dynamic
                return;
            }
            else if (ignoreSimpleValueResults && result.BindingTime == BindingTime.StaticSimpleValue)
            {
                // If the expression has a simple statically-bound value, it will be replaced with a constant representation of that value during the evaluation,
                // and thus the complex-valued symbols that were used to extract it do not need to be converted to dynamically-valued
                return;
            }

            foreach (Symbol symbol in result.ComplexValuedSymbols)
            {
                MakeSymbolDynamic(symbol);
            }
        }

        private void MakeMainSymbolDynamic(BindingTimeAnalysisResult result)
        {
            if (result.MainSymbol != null)
            {
                MakeSymbolDynamic(result.MainSymbol);
            }
        }

        private void MakeSymbolDynamic(Symbol symbol)
        {
            if (_hasChangesInVariableBindingTimes)
            {
                _variableBindingTimes = _variableBindingTimes.SetItem(symbol, BindingTime.Dynamic);
            }
            else
            {
                ImmutableDictionary<Symbol, BindingTime> oldVariableBindingTimes = _variableBindingTimes;
                _variableBindingTimes = _variableBindingTimes.SetItem(symbol, BindingTime.Dynamic);
                _hasChangesInVariableBindingTimes = (_variableBindingTimes != oldVariableBindingTimes);
            }
        }

        private BindingTimeAnalysisResult GetCompoundResult(ImmutableArray<BindingTimeAnalysisResult> results)
        {
            BindingTime unifiedBindingTime = BindingTime.StaticSimpleValue;
            ImmutableHashSet<Symbol> complexValuedSymbols = ImmutableHashSet<Symbol>.Empty;
            if (!results.IsDefaultOrEmpty)
            {
                for (int i = 0; i < results.Length; i++)
                {
                    switch (results[i].BindingTime)
                    {
                        case BindingTime.Dynamic:
                            unifiedBindingTime = BindingTime.Dynamic;
                            break;

                        case BindingTime.StaticComplexValue:
                        case BindingTime.StaticArgumentArray:
                            if (unifiedBindingTime != BindingTime.Dynamic)
                            {
                                unifiedBindingTime = BindingTime.StaticComplexValue;
                                complexValuedSymbols = complexValuedSymbols.Union(results[i].ComplexValuedSymbols);
                            }
                            break;
                    }
                }
            }
            if (unifiedBindingTime == BindingTime.Dynamic)
            {
                MakeComplexValuedSymbolsDynamic(results);
                return new BindingTimeAnalysisResult(BindingTime.Dynamic);
            }
            else
            {
                return new BindingTimeAnalysisResult(unifiedBindingTime, null, complexValuedSymbols);
            }
        }

        private BindingTimeAnalysisResult GetCompoundResult(params BindingTimeAnalysisResult[] results)
        {
            return GetCompoundResult(results.ToImmutableArray());
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
            if (call.Method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__Invoke))
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
