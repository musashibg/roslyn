using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    partial class DecorationRewriter
    {
        public override DecorationRewriteResult VisitCall(BoundCall node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            MethodSymbol method = node.Method;

            // Handle splice locations
            if (CheckIsSpliceLocation(node))
            {
                BoundExpression rewrittenNode;
                bool mustEmit;
                if (_flags.HasFlag(DecorationRewriterFlags.ProhibitSpliceLocation))
                {
                    _diagnostics.Add(ErrorCode.ERR_InvalidSpliceLocation, node.Syntax.Location);
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    mustEmit = true;
                }
                else
                {
                    // Prepare a temporary variable for the result (replace void with object to maintain the semantics of reflection method invocation)
                    LocalSymbol resultLocal = _targetMethod.ReturnsVoid
                                                ? null
                                                : _factory.SynthesizedLocal(
                                                    _targetMethod.ReturnType,
                                                    node.Syntax,
                                                    kind: SynthesizedLocalKind.DecoratorTempResult,
                                                    name: _variableNameGenerator.GenerateFreshName("tempResult"));

                    // Prepare the spliced method body, which will be inserted prior to the current statement
                    BoundExpression valueExpression = null;
                    if (_targetMemberKind == DecoratedMemberKind.IndexerSet || _targetMemberKind == DecoratedMemberKind.PropertySet)
                    {
                        valueExpression = node.Arguments[1];
                    }
                    BoundExpression argumentArray = null;
                    if (_targetMemberKind != DecoratedMemberKind.Destructor && _targetMemberKind != DecoratedMemberKind.PropertyGet && _targetMemberKind != DecoratedMemberKind.PropertySet)
                    {
                        argumentArray = node.Arguments[node.Arguments.Length - 1];
                    }
                    SpliceMethodBody(resultLocal, valueExpression, argumentArray, node.Syntax, variableValues);

                    // Replace the method invocation call with a reference to the result variable
                    if (resultLocal == null)
                    {
                        rewrittenNode = new BoundLiteral(node.Syntax, ConstantValue.Null, node.Type) { WasCompilerGenerated = true };
                        mustEmit = false;
                    }
                    else
                    {
                        rewrittenNode = MetaUtils.ConvertIfNeeded(
                            node.Type,
                            new BoundLocal(node.Syntax, resultLocal, null, resultLocal.Type) { WasCompilerGenerated = true },
                            _compilation);
                        mustEmit = false;
                    }
                }
                return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, CompileTimeValue.Dynamic);
            }

            // Handle base method calls (forbidden)
            if (CheckIsBaseDecoratorMethodCall(node))
            {
                _diagnostics.Add(ErrorCode.ERR_BaseDecoratorMethodCallNotSupported, node.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(node.Syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                variableValues = receiverResult.UpdatedVariableValues;
            }

            ImmutableArray<DecorationRewriteResult> argumentsResults = VisitSequentialList(node.Arguments, ref variableValues);

            // Handle well-known method invocations with static binding time
            if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetConstructors)
                || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetConstructors2))
            {
                return VisitGetConstructors(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetMethods)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetMethods2))
            {
                return VisitGetMethods(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetProperties)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetProperties2))
            {
                return VisitGetProperties(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAssignableFrom)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaExtensions__IsAssignableFrom))
            {
                return VisitIsAssignableFromCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__GetParameters))
            {
                return VisitGetParametersCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetAccessors))
            {
                return VisitGetAccessorsCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue))
            {
                return VisitGetValueCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue))
            {
                return VisitSetValueCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T)
                     || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2))
            {
                return VisitGetCustomAttributesCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__CloneArguments))
            {
                return VisitCloneArgumentsCall(node, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__CloneArgumentsToObjectArray))
            {
                return VisitCloneArgumentsToObjectArrayCall(node, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__DefaultValue))
            {
                return VisitDefaultValueCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__IsReadOnly))
            {
                return VisitIsReadOnlyCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__IsWriteOnly))
            {
                return VisitIsWriteOnlyCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType2))
            {
                return VisitParameterTypeCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType2))
            {
                return VisitThisObjectTypeCall(node, receiverResult, argumentsResults, variableValues);
            }

            // If the method being invoked is not any of the specially-handled methods, emit it unchanged
            return new DecorationRewriteResult(
                node.Update(
                    (BoundExpression)receiverResult?.Node,
                    node.Method,
                    argumentsResults.SelectAsArray(r => (BoundExpression)r.Node),
                    node.ArgumentNamesOpt,
                    node.ArgumentRefKindsOpt,
                    node.IsDelegateCall,
                    node.Expanded,
                    node.InvokedAsExtensionMethod,
                    node.ArgsToParamsOpt,
                    node.ResultKind,
                    node.Type),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        private bool CheckIsSpliceLocation(BoundCall call)
        {
            MethodSymbol method = call.Method;
            switch (_targetMemberKind)
            {
                case DecoratedMemberKind.Constructor:
                case DecoratedMemberKind.Method:
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
                            // Disallow calls to MethodBase.Invoke(object obj, object[] parameters) which are not obvious splices
                            // (as they might use a different thisObject, or they might refer to this method through a different local variable, leading to infinite recursion)
                            _diagnostics.Add(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.Destructor:
                    if (call.Method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__Invoke))
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
                            _diagnostics.Add(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.IndexerGet:
                    if (call.Method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue2))
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
                            _diagnostics.Add(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.IndexerSet:
                    if (call.Method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue2))
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
                            _diagnostics.Add(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.PropertyGet:
                    if (call.Method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetValue))
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
                            _diagnostics.Add(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
                        }
                    }
                    break;

                case DecoratedMemberKind.PropertySet:
                    if (call.Method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__SetValue))
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
                            _diagnostics.Add(ErrorCode.ERR_InvalidSpecialMethodCallInDecorator, call.Syntax.Location, method);
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

        private void SpliceMethodBody(
            LocalSymbol tempResultLocal,
            BoundExpression valueExpression,
            BoundExpression argumentsArray,
            CSharpSyntaxNode syntax,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // All splice locations in the decorator method's body should be inside a block
            Debug.Assert(_blockLocalsBuilder != null);

            var postSpliceLabel = _factory.GenerateLabel($"decorator_{_decoratorOrdinal}_post_splice_{_spliceOrdinal}");

            var parameterReplacementsBuilder = ImmutableDictionary.CreateBuilder<Symbol, Symbol>();
            if (_targetMemberKind == DecoratedMemberKind.IndexerSet || _targetMemberKind == DecoratedMemberKind.PropertySet)
            {
                ParameterSymbol valueParameter = _targetMethod.Parameters[_argumentCount];

                // Handle property/indexer value parameter
                Debug.Assert(valueExpression != null);
                Symbol valueSymbol = null;
                TypeSymbol valueSymbolType = null;
                if (valueExpression is BoundParameter)
                {
                    ParameterSymbol parameter = ((BoundParameter)valueExpression).ParameterSymbol;
                    if (parameter == _decoratorMethod.Parameters[2])
                    {
                        valueSymbolType = valueParameter.Type;
                        valueSymbol = valueParameter;
                    }
                }
                else if (valueExpression is BoundLocal)
                {
                    LocalSymbol local = ((BoundLocal)valueExpression).LocalSymbol;
                    valueSymbolType = local.Type;
                    valueSymbol = GetReplacementSymbol(local);
                }

                if (valueSymbol != null && valueSymbolType == valueParameter.Type)
                {
                    parameterReplacementsBuilder.Add(valueParameter, valueSymbol);
                }
                else
                {
                    DecorationRewriteResult valueExpressionResult = Visit(valueExpression, variableValues);
                    variableValues = valueExpressionResult.UpdatedVariableValues;

                    LocalSymbol valueParameterReplacementLocal = _factory.SynthesizedLocal(
                        valueParameter.Type,
                        syntax: syntax,
                        kind: SynthesizedLocalKind.DecoratedMethodParameter,
                        name: _variableNameGenerator.GenerateFreshName(valueParameter.Name));

                    parameterReplacementsBuilder.Add(valueParameter, valueParameterReplacementLocal);

                    _blockLocalsBuilder.Add(valueParameterReplacementLocal);

                    _splicedStatementsBuilder.Add(
                        new BoundLocalDeclaration(
                            syntax,
                            valueParameterReplacementLocal,
                            new BoundTypeExpression(syntax, null, false, null, valueParameterReplacementLocal.Type) { WasCompilerGenerated = true },
                            MetaUtils.ConvertIfNeeded(
                                valueParameterReplacementLocal.Type,
                                (BoundExpression)valueExpressionResult.Node,
                                _compilation),
                            default(ImmutableArray<BoundExpression>))
                        {
                            WasCompilerGenerated = true,
                        });
                }
            }

            LocalSymbol[] parameterReplacementLocals = null;
            BoundLocal argumentsArrayLocalNode = null;
            if (_targetMemberKind != DecoratedMemberKind.Destructor && _targetMemberKind != DecoratedMemberKind.PropertyGet && _targetMemberKind != DecoratedMemberKind.PropertySet)
            {
                // Handle argument array
                Debug.Assert(argumentsArray != null);
                Symbol argumentsArraySymbol;
                if (argumentsArray is BoundParameter)
                {
                    argumentsArraySymbol = ((BoundParameter)argumentsArray).ParameterSymbol;
                }
                else
                {
                    Debug.Assert(argumentsArray is BoundLocal);
                    argumentsArraySymbol = ((BoundLocal)argumentsArray).LocalSymbol;
                }
                CompileTimeValue argumentsArrayValue = variableValues[argumentsArraySymbol];

                if (argumentsArrayValue.Kind == CompileTimeValueKind.ArgumentArray)
                {
                    var staticArgumentsArray = (ArgumentArrayValue)argumentsArrayValue;

                    // Prepare parameter replacements dictionary
                    for (int i = 0; i < _argumentCount; i++)
                    {
                        parameterReplacementsBuilder.Add(_targetMethod.Parameters[i], staticArgumentsArray.ArgumentSymbols[i]);
                    }
                }
                else
                {
                    Debug.Assert(argumentsArrayValue.Kind == CompileTimeValueKind.Dynamic);
                    var argumentsArrayLocal = (LocalSymbol)GetReplacementSymbol(argumentsArraySymbol);
                    argumentsArrayLocalNode = new BoundLocal(syntax, argumentsArrayLocal, null, argumentsArrayLocal.Type) { WasCompilerGenerated = true };

                    // Declare fresh variables that will replace the spliced method body's parameters and generate assignments for them
                    parameterReplacementLocals = new LocalSymbol[_argumentCount];
                    for (int i = 0; i < _argumentCount; i++)
                    {
                        ParameterSymbol parameter = _targetMethod.Parameters[i];
                        LocalSymbol parameterReplacementLocal = _factory.SynthesizedLocal(
                            parameter.Type,
                            syntax: syntax,
                            kind: SynthesizedLocalKind.DecoratedMethodParameter,
                        name: _variableNameGenerator.GenerateFreshName(parameter.Name));
                        parameterReplacementLocals[i] = parameterReplacementLocal;
                        parameterReplacementsBuilder.Add(parameter, parameterReplacementLocal);
                        _blockLocalsBuilder.Add(parameterReplacementLocal);

                        _splicedStatementsBuilder.Add(
                            new BoundLocalDeclaration(
                                syntax,
                                parameterReplacementLocal,
                                new BoundTypeExpression(syntax, null, false, null, parameterReplacementLocal.Type) { WasCompilerGenerated = true },
                                MetaUtils.ConvertIfNeeded(
                                    parameterReplacementLocal.Type,
                                    new BoundArrayAccess(
                                        syntax,
                                        argumentsArrayLocalNode,
                                        ImmutableArray.Create<BoundExpression>(
                                            new BoundLiteral(syntax, ConstantValue.Create(i, SpecialType.System_Int32), _compilation.GetSpecialType(SpecialType.System_Int32))
                                            {
                                                WasCompilerGenerated = true,
                                            }),
                                        _compilation.GetSpecialType(SpecialType.System_Object))
                                    {
                                        WasCompilerGenerated = true,
                                    },
                                    _compilation),
                                default(ImmutableArray<BoundExpression>))
                            {
                                WasCompilerGenerated = true,
                            });
                    }
                }
            }

            // Insert temporary result local variable declaration
            if (tempResultLocal != null)
            {
                _blockLocalsBuilder.Add(tempResultLocal);
                _splicedStatementsBuilder.Add(
                    new BoundLocalDeclaration(
                        syntax,
                        tempResultLocal,
                        new BoundTypeExpression(syntax, null, tempResultLocal.Type) { WasCompilerGenerated = true },
                        null,
                        default(ImmutableArray<BoundExpression>))
                    {
                        WasCompilerGenerated = true,
                    });
            }

            // Insert the spliced method body
            var spliceRewrittenBody = SplicedMethodBodyRewriter.Rewrite(
                _factory,
                tempResultLocal,
                postSpliceLabel,
                _spliceOrdinal,
                _targetBody,
                parameterReplacementsBuilder.ToImmutable(),
                _variableNameGenerator,
                _diagnostics);
            _splicedStatementsBuilder.Add(spliceRewrittenBody);

            // Insert the post-splice label
            _splicedStatementsBuilder.Add(new BoundLabelStatement(syntax, postSpliceLabel) { WasCompilerGenerated = true });

            // If temporary replacement variables were created for the spliced method body's parameters, we copy their final values back to the dynamic argument array
            if (parameterReplacementLocals != null)
            {
                for (int i = 0; i < _argumentCount; i++)
                {
                    LocalSymbol parameterReplacementLocal = parameterReplacementLocals[i];
                    _splicedStatementsBuilder.Add(
                        new BoundExpressionStatement(
                            syntax,
                            new BoundAssignmentOperator(
                                syntax,
                                new BoundArrayAccess(
                                    syntax,
                                    argumentsArrayLocalNode,
                                    ImmutableArray.Create<BoundExpression>(
                                        new BoundLiteral(syntax, ConstantValue.Create(i, SpecialType.System_Int32), _compilation.GetSpecialType(SpecialType.System_Int32))
                                        {
                                            WasCompilerGenerated = true,
                                        }),
                                    _compilation.GetSpecialType(SpecialType.System_Object))
                                {
                                    WasCompilerGenerated = true,
                                },
                                MetaUtils.ConvertIfNeeded(
                                    _compilation.ObjectType,
                                    new BoundLocal(syntax, parameterReplacementLocal, null, parameterReplacementLocal.Type) { WasCompilerGenerated = true },
                                    _compilation),
                                RefKind.None,
                                _compilation.ObjectType)
                            {
                                WasCompilerGenerated = true,
                            })
                        {
                            WasCompilerGenerated = true,
                        });
                }
            }

            _spliceOrdinal++;
        }

        private DecorationRewriteResult VisitGetConstructors(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.Length <= 1);
            CompileTimeValue receiverValue = receiverResult.Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            if (receiverValue.Kind == CompileTimeValueKind.Dynamic
                || (!argumentsResults.IsEmpty && argumentsResults[0].Value.Kind == CompileTimeValueKind.Dynamic))
            {
                value = CompileTimeValue.Dynamic;
            }
            else
            {
                Debug.Assert(receiverValue is TypeValue && node.Type.IsArray());
                TypeSymbol type = ((TypeValue)receiverValue).Type;

                BindingFlags bindingFlags;
                if (argumentsResults.IsEmpty)
                {
                    bindingFlags = BindingFlags.Instance | BindingFlags.Public;
                }
                else
                {
                    var bindingFlagsValue = argumentsResults[0].Value as EnumValue;
                    Debug.Assert(bindingFlagsValue != null && bindingFlagsValue.EnumType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags));
                    bindingFlags = (BindingFlags)bindingFlagsValue.UnderlyingValue.Int32Value;
                }

                value = StaticValueUtils.LookupConstructors(((TypeValue)receiverValue).Type, bindingFlags, (ArrayTypeSymbol)node.Type);
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                value.Kind == CompileTimeValueKind.Dynamic,
                value);
        }

        private DecorationRewriteResult VisitGetMethods(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.Length <= 1);
            CompileTimeValue receiverValue = receiverResult.Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            if (receiverValue.Kind == CompileTimeValueKind.Dynamic
                || (!argumentsResults.IsEmpty && argumentsResults[0].Value.Kind == CompileTimeValueKind.Dynamic))
            {
                value = CompileTimeValue.Dynamic;
            }
            else
            {
                Debug.Assert(receiverValue is TypeValue && node.Type.IsArray());
                TypeSymbol type = ((TypeValue)receiverValue).Type;

                BindingFlags bindingFlags;
                if (argumentsResults.IsEmpty)
                {
                    bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
                }
                else
                {
                    var bindingFlagsValue = argumentsResults[0].Value as EnumValue;
                    Debug.Assert(bindingFlagsValue != null && bindingFlagsValue.EnumType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags));
                    bindingFlags = (BindingFlags)bindingFlagsValue.UnderlyingValue.Int32Value;
                }

                value = StaticValueUtils.LookupMethods(((TypeValue)receiverValue).Type, bindingFlags, (ArrayTypeSymbol)node.Type);
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                value.Kind == CompileTimeValueKind.Dynamic,
                value);
        }

        private DecorationRewriteResult VisitGetProperties(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.Length <= 1);
            CompileTimeValue receiverValue = receiverResult.Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            if (receiverValue.Kind == CompileTimeValueKind.Dynamic
                || (!argumentsResults.IsEmpty && argumentsResults[0].Value.Kind == CompileTimeValueKind.Dynamic))
            {
                value = CompileTimeValue.Dynamic;
            }
            else
            {
                Debug.Assert(receiverValue is TypeValue && node.Type.IsArray());
                TypeSymbol type = ((TypeValue)receiverValue).Type;

                BindingFlags bindingFlags;
                if (argumentsResults.IsEmpty)
                {
                    bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
                }
                else
                {
                    var bindingFlagsValue = argumentsResults[0].Value as EnumValue;
                    Debug.Assert(bindingFlagsValue != null && bindingFlagsValue.EnumType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags));
                    bindingFlags = (BindingFlags)bindingFlagsValue.UnderlyingValue.Int32Value;
                }

                value = StaticValueUtils.LookupProperties(((TypeValue)receiverValue).Type, bindingFlags, (ArrayTypeSymbol)node.Type);
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                value.Kind == CompileTimeValueKind.Dynamic,
                value);
        }

        private DecorationRewriteResult VisitIsAssignableFromCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            CompileTimeValue targetTypeValue;
            CompileTimeValue sourceTypeValue;
            bool isExtensionMethod;
            MethodSymbol method = node.Method;
            CSharpSyntaxNode syntax = node.Syntax;
            if (method.IsStatic)
            {
                Debug.Assert(argumentsResults.Length == 2);
                targetTypeValue = argumentsResults[0].Value;
                sourceTypeValue = argumentsResults[1].Value;
                isExtensionMethod = true;
            }
            else
            {
                Debug.Assert(receiverResult != null && argumentsResults.Length == 1);
                targetTypeValue = receiverResult.Value;
                sourceTypeValue = argumentsResults[0].Value;
                isExtensionMethod = false;
            }

            if (targetTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)targetTypeValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, (isExtensionMethod ? node.Arguments[0] : node.ReceiverOpt).Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            if (sourceTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)sourceTypeValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, (isExtensionMethod ? node.Arguments[1] : node.Arguments[0]).Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            if (targetTypeValue.Kind == CompileTimeValueKind.Dynamic || sourceTypeValue.Kind == CompileTimeValueKind.Dynamic)
            {
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            Debug.Assert(targetTypeValue is TypeValue && sourceTypeValue is TypeValue);
            var value = new ConstantStaticValue(
                ConstantValue.Create(
                    MetaUtils.CheckTypeIsAssignableFrom(((TypeValue)targetTypeValue).Type, ((TypeValue)sourceTypeValue).Type)));
            return new DecorationRewriteResult(
                MakeSimpleStaticValueExpression(value, node.Type, node.Syntax),
                variableValues,
                false,
                value);
        }

        private DecorationRewriteResult VisitGetParametersCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.IsEmpty);
            CompileTimeValue receiverValue = receiverResult.Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            if (receiverValue.Kind == CompileTimeValueKind.Complex)
            {
                MethodSymbol method;
                if (receiverValue is MethodInfoValue)
                {
                    method = ((MethodInfoValue)receiverValue).Method;
                }
                else
                {
                    Debug.Assert(receiverValue is ConstructorInfoValue);
                    method = ((ConstructorInfoValue)receiverValue).Constructor;
                }
                value = new ArrayValue(
                    _compilation.CreateArrayTypeSymbol(_compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo)),
                    method.Parameters.SelectAsArray(p => (CompileTimeValue)(new ParameterInfoValue(p))));
            }
            else
            {
                Debug.Assert(receiverValue.Kind == CompileTimeValueKind.Dynamic);
                value = CompileTimeValue.Dynamic;
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                value.Kind == CompileTimeValueKind.Dynamic,
                value);
        }

        private DecorationRewriteResult VisitGetAccessorsCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.IsEmpty);
            CompileTimeValue receiverValue = receiverResult.Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            if (receiverValue.Kind == CompileTimeValueKind.Complex)
            {
                Debug.Assert(receiverValue is PropertyInfoValue);
                PropertySymbol property = ((PropertyInfoValue)receiverValue).Property;

                ImmutableArray<CompileTimeValue>.Builder accessorsBuilder = ImmutableArray.CreateBuilder<CompileTimeValue>();
                if (property.GetMethod != null)
                {
                    accessorsBuilder.Add(new MethodInfoValue(property.GetMethod));
                }
                if (property.SetMethod != null)
                {
                    accessorsBuilder.Add(new MethodInfoValue(property.SetMethod));
                }
                value = new ArrayValue(
                    _compilation.CreateArrayTypeSymbol(_compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo)),
                    accessorsBuilder.ToImmutable());
            }
            else
            {
                Debug.Assert(receiverValue.Kind == CompileTimeValueKind.Dynamic);
                value = CompileTimeValue.Dynamic;
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                value.Kind == CompileTimeValueKind.Dynamic,
                value);
        }

        private DecorationRewriteResult VisitGetValueCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.Length == 1);
            CompileTimeValue receiverValue = receiverResult.Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            BoundExpression rewrittenNode;
            bool mustEmit;
            if (receiverValue.Kind == CompileTimeValueKind.Complex)
            {
                Debug.Assert(receiverValue is PropertyInfoValue);
                PropertySymbol property = ((PropertyInfoValue)receiverValue).Property;

                rewrittenNode = MetaUtils.ConvertIfNeeded(
                    node.Type,
                    new BoundPropertyAccess(
                        node.Syntax,
                        MetaUtils.ConvertIfNeeded(property.ContainingType, (BoundExpression)argumentsResults[0].Node, _compilation),
                        property,
                        LookupResultKind.Viable,
                        property.Type)
                    {
                        WasCompilerGenerated = true,
                    },
                    _compilation);
                mustEmit = false;
            }
            else
            {
                Debug.Assert(receiverValue.Kind == CompileTimeValueKind.Dynamic);
                rewrittenNode = node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node));
                mustEmit = true;
            }

            return new DecorationRewriteResult(rewrittenNode, variableValues, mustEmit, CompileTimeValue.Dynamic);
        }

        private DecorationRewriteResult VisitSetValueCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.Length == 2);
            CompileTimeValue receiverValue = receiverResult.Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            BoundExpression rewrittenNode;
            if (receiverValue.Kind == CompileTimeValueKind.Complex)
            {
                Debug.Assert(receiverValue is PropertyInfoValue);
                PropertySymbol property = ((PropertyInfoValue)receiverValue).Property;

                rewrittenNode = new BoundAssignmentOperator(
                    node.Syntax,
                    new BoundPropertyAccess(
                        node.Syntax,
                        MetaUtils.ConvertIfNeeded(property.ContainingType, (BoundExpression)argumentsResults[0].Node, _compilation),
                        property,
                        LookupResultKind.Viable,
                        property.Type)
                    {
                        WasCompilerGenerated = true,
                    },
                    MetaUtils.ConvertIfNeeded(property.Type, (BoundExpression)argumentsResults[1].Node, _compilation),
                    RefKind.None,
                    property.Type)
                {
                    WasCompilerGenerated = true,
                };
            }
            else
            {
                Debug.Assert(receiverValue.Kind == CompileTimeValueKind.Dynamic);
                rewrittenNode = node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node));
            }

            return new DecorationRewriteResult(rewrittenNode, variableValues, true, CompileTimeValue.Dynamic);
        }

        private DecorationRewriteResult VisitGetCustomAttributesCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 1 && node.Method.TypeArguments.Length == 1);
            CompileTimeValue argumentValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (argumentValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)argumentValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            BoundExpression rewrittenNode = null;
            MethodSymbol invokedMethod = node.Method;
            TypeSymbol requestedAttributeType = invokedMethod.TypeArguments[0];
            CSharpAttributeData candidateAttribute = null;
            if (argumentValue.Kind == CompileTimeValueKind.Dynamic)
            {
                value = CompileTimeValue.Dynamic;
                rewrittenNode = node.Update((BoundExpression)receiverResult?.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node));
            }
            else
            {
                try
                {
                    if (argumentValue.Kind == CompileTimeValueKind.Simple)
                    {
                        Debug.Assert(argumentValue is TypeValue
                                     && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                        TypeSymbol type = ((TypeValue)argumentValue).Type;
                        value = StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, type.GetAttributes(), _diagnostics, out candidateAttribute);
                    }
                    else
                    {
                        Debug.Assert(argumentValue.Kind == CompileTimeValueKind.Complex);
                        if (argumentValue is MethodInfoValue)
                        {
                            Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                            MethodSymbol method = ((MethodInfoValue)argumentValue).Method;
                            value = StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, method.GetAttributes(), _diagnostics, out candidateAttribute);
                        }
                        else if (argumentValue is ConstructorInfoValue)
                        {
                            Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                            MethodSymbol constructor = ((ConstructorInfoValue)argumentValue).Constructor;
                            value = StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, constructor.GetAttributes(), _diagnostics, out candidateAttribute);
                        }
                        else if (argumentValue is PropertyInfoValue)
                        {
                            Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                            PropertySymbol property = ((PropertyInfoValue)argumentValue).Property;
                            value = StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, property.GetAttributes(), _diagnostics, out candidateAttribute);
                        }
                        else
                        {
                            Debug.Assert(argumentValue is ParameterInfoValue
                                         && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2));
                            ParameterSymbol parameter = ((ParameterInfoValue)argumentValue).Parameter;
                            value = StaticValueUtils.LookupCustomAttributeValue(node.Syntax, requestedAttributeType, parameter.GetAttributes(), _diagnostics, out candidateAttribute);
                        }
                    }


                    if (value.Kind == CompileTimeValueKind.Simple)
                    {
                        rewrittenNode = MakeSimpleStaticValueExpression(value, requestedAttributeType, syntax);
                    }
                    else
                    {
                        Debug.Assert(value.Kind == CompileTimeValueKind.Complex);
                        rewrittenNode = MakeAttributeCreationExpression(syntax, candidateAttribute, requestedAttributeType);
                    }
                }
                catch (ExecutionInterruptionException)
                {
                    // Ambiguous match was encountered
                    value = CompileTimeValue.Dynamic;
                    rewrittenNode = MakeBadExpression(syntax, requestedAttributeType);
                }
            }
            return new DecorationRewriteResult(rewrittenNode, variableValues, value.Kind == CompileTimeValueKind.Dynamic, value);
        }

        private DecorationRewriteResult VisitCloneArgumentsCall(
            BoundCall node,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 1);
            CompileTimeValue argumentArrayValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (_flags.HasFlag(DecorationRewriterFlags.ExpectedDynamicArgumentArray))
            {
                var boundsExpression = new BoundLiteral(
                    syntax,
                    ConstantValue.Create(_argumentCount),
                    _compilation.GetSpecialType(SpecialType.System_Int32))
                {
                    WasCompilerGenerated = true,
                };

                BoundArrayInitialization arrayInitialization = null;
                if (_argumentCount > 0)
                {
                    var arrayInitializationExpressions = new BoundExpression[_argumentCount];

                    if (argumentArrayValue.Kind == CompileTimeValueKind.ArgumentArray)
                    {
                        Debug.Assert(argumentArrayValue is ArgumentArrayValue);
                        ImmutableArray<Symbol> argumentSymbols = ((ArgumentArrayValue)argumentArrayValue).ArgumentSymbols;
                        for (int i = 0; i < _argumentCount; i++)
                        {
                            Symbol argumentSymbol = argumentSymbols[i];
                            BoundExpression argumentSymbolExpression;
                            if (argumentSymbol.Kind == SymbolKind.Parameter)
                            {
                                argumentSymbolExpression = new BoundParameter(syntax, (ParameterSymbol)argumentSymbol) { WasCompilerGenerated = true };
                            }
                            else
                            {
                                Debug.Assert(argumentSymbol.Kind == SymbolKind.Local);
                                argumentSymbolExpression = new BoundLocal(syntax, (LocalSymbol)argumentSymbol, null, ((LocalSymbol)argumentSymbol).Type) { WasCompilerGenerated = true };
                            }
                            arrayInitializationExpressions[i] = MetaUtils.ConvertIfNeeded(_compilation.ObjectType, argumentSymbolExpression, _compilation);
                        }
                    }
                    else
                    {
                        Debug.Assert(argumentArrayValue.Kind == CompileTimeValueKind.Dynamic);
                        var argumentArrayExpression = (BoundExpression)argumentsResults[0].Node;
                        for (int i = 0; i < _argumentCount; i++)
                        {
                            arrayInitializationExpressions[i] = new BoundArrayAccess(
                                syntax,
                                argumentArrayExpression,
                                ImmutableArray.Create<BoundExpression>(
                                    new BoundLiteral(syntax, ConstantValue.Create(i), _compilation.GetSpecialType(SpecialType.System_Int32)) { WasCompilerGenerated = true }),
                                _compilation.ObjectType)
                            {
                                WasCompilerGenerated = true,
                            };
                        }
                    }
                    arrayInitialization = new BoundArrayInitialization(syntax, arrayInitializationExpressions.ToImmutableArray()) { WasCompilerGenerated = true };
                }

                var rewrittenNode = new BoundArrayCreation(
                    syntax,
                    ImmutableArray.Create<BoundExpression>(boundsExpression),
                    arrayInitialization,
                    _compilation.CreateArrayTypeSymbol(_compilation.ObjectType))
                {
                    WasCompilerGenerated = true,
                };

                return new DecorationRewriteResult(rewrittenNode, variableValues, true, CompileTimeValue.Dynamic);
            }
            else
            {
                // Declare fresh variables that will hold the cloned argument values
                var newArgumentLocals = new LocalSymbol[_argumentCount];
                for (int i = 0; i < _argumentCount; i++)
                {
                    ParameterSymbol parameter = _targetMethod.Parameters[i];
                    TypeSymbol parameterType = parameter.Type;
                    LocalSymbol newArgumentLocal = _factory.SynthesizedLocal(
                        parameterType,
                        syntax: syntax,
                        kind: SynthesizedLocalKind.DecoratedMethodParameter,
                        name: _variableNameGenerator.GenerateFreshName(parameter.Name));
                    newArgumentLocals[i] = newArgumentLocal;
                    _blockLocalsBuilder.Add(newArgumentLocal);

                    BoundExpression originalArgumentExpression;
                    if (argumentArrayValue.Kind == CompileTimeValueKind.ArgumentArray)
                    {
                        Debug.Assert(argumentArrayValue is ArgumentArrayValue);
                        ImmutableArray<Symbol> argumentSymbols = ((ArgumentArrayValue)argumentArrayValue).ArgumentSymbols;
                        Symbol argumentSymbol = argumentSymbols[i];
                        if (argumentSymbol.Kind == SymbolKind.Parameter)
                        {
                            originalArgumentExpression = new BoundParameter(syntax, (ParameterSymbol)argumentSymbol) { WasCompilerGenerated = true };
                        }
                        else
                        {
                            Debug.Assert(argumentSymbol.Kind == SymbolKind.Local);
                            originalArgumentExpression = new BoundLocal(syntax, (LocalSymbol)argumentSymbol, null, ((LocalSymbol)argumentSymbol).Type) { WasCompilerGenerated = true };
                        }
                    }
                    else
                    {
                        Debug.Assert(argumentArrayValue.Kind == CompileTimeValueKind.Dynamic);
                        var argumentArrayExpression = (BoundExpression)argumentsResults[0].Node;
                        originalArgumentExpression = MetaUtils.ConvertIfNeeded(
                            parameterType,
                            new BoundArrayAccess(
                                syntax,
                                argumentArrayExpression,
                                ImmutableArray.Create<BoundExpression>(
                                    new BoundLiteral(syntax, ConstantValue.Create(i), _compilation.GetSpecialType(SpecialType.System_Int32)) { WasCompilerGenerated = true }),
                                _compilation.ObjectType)
                            {
                                WasCompilerGenerated = true,
                            },
                            _compilation);
                    }

                    _splicedStatementsBuilder.Add(
                        new BoundLocalDeclaration(
                            syntax,
                            newArgumentLocal,
                            new BoundTypeExpression(syntax, null, false, null, newArgumentLocal.Type) { WasCompilerGenerated = true },
                            originalArgumentExpression,
                            default(ImmutableArray<BoundExpression>))
                        {
                            WasCompilerGenerated = true,
                        });
                }

                return new DecorationRewriteResult(
                    MakeBadExpression(syntax, node.Type),
                    variableValues,
                    false,
                    new ArgumentArrayValue(newArgumentLocals.ToImmutableArray<Symbol>()));
            }
        }

        private DecorationRewriteResult VisitCloneArgumentsToObjectArrayCall(
            BoundCall node,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 1);
            CompileTimeValue argumentArrayValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            var boundsExpression = new BoundLiteral(
                syntax,
                ConstantValue.Create(_argumentCount),
                _compilation.GetSpecialType(SpecialType.System_Int32))
            {
                WasCompilerGenerated = true,
            };

            BoundArrayInitialization arrayInitialization = null;
            if (_argumentCount > 0)
            {
                var arrayInitializationExpressions = new BoundExpression[_argumentCount];

                if (argumentArrayValue.Kind == CompileTimeValueKind.ArgumentArray)
                {
                    Debug.Assert(argumentArrayValue is ArgumentArrayValue);
                    ImmutableArray<Symbol> argumentSymbols = ((ArgumentArrayValue)argumentArrayValue).ArgumentSymbols;
                    for (int i = 0; i < _argumentCount; i++)
                    {
                        Symbol argumentSymbol = argumentSymbols[i];
                        BoundExpression argumentSymbolExpression;
                        if (argumentSymbol.Kind == SymbolKind.Parameter)
                        {
                            argumentSymbolExpression = new BoundParameter(syntax, (ParameterSymbol)argumentSymbol) { WasCompilerGenerated = true };
                        }
                        else
                        {
                            Debug.Assert(argumentSymbol.Kind == SymbolKind.Local);
                            argumentSymbolExpression = new BoundLocal(syntax, (LocalSymbol)argumentSymbol, null, ((LocalSymbol)argumentSymbol).Type) { WasCompilerGenerated = true };
                        }
                        arrayInitializationExpressions[i] = MetaUtils.ConvertIfNeeded(_compilation.ObjectType, argumentSymbolExpression, _compilation);
                    }
                }
                else
                {
                    Debug.Assert(argumentArrayValue.Kind == CompileTimeValueKind.Dynamic);
                    var argumentArrayExpression = (BoundExpression)argumentsResults[0].Node;
                    for (int i = 0; i < _argumentCount; i++)
                    {
                        arrayInitializationExpressions[i] = new BoundArrayAccess(
                            syntax,
                            argumentArrayExpression,
                            ImmutableArray.Create<BoundExpression>(
                                new BoundLiteral(syntax, ConstantValue.Create(i), _compilation.GetSpecialType(SpecialType.System_Int32)) { WasCompilerGenerated = true }),
                            _compilation.ObjectType)
                        {
                            WasCompilerGenerated = true,
                        };
                    }
                }
                arrayInitialization = new BoundArrayInitialization(syntax, arrayInitializationExpressions.ToImmutableArray()) { WasCompilerGenerated = true };
            }

            var rewrittenNode = new BoundArrayCreation(
                syntax,
                ImmutableArray.Create<BoundExpression>(boundsExpression),
                arrayInitialization,
                _compilation.CreateArrayTypeSymbol(_compilation.ObjectType))
            {
                WasCompilerGenerated = true,
            };

            return new DecorationRewriteResult(rewrittenNode, variableValues, true, CompileTimeValue.Dynamic);
        }

        private DecorationRewriteResult VisitDefaultValueCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 1);
            CompileTimeValue typeValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (typeValue.Kind != CompileTimeValueKind.Dynamic)
            {
                if (typeValue is ConstantStaticValue)
                {
                    Debug.Assert(((ConstantStaticValue)typeValue).Value.IsNull);
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                    return new DecorationRewriteResult(
                        MakeBadExpression(syntax, node.Type),
                        variableValues,
                        true,
                        CompileTimeValue.Dynamic);
                }

                Debug.Assert(typeValue is TypeValue);
                TypeSymbol type = ((TypeValue)typeValue).Type;

                BoundExpression rewrittenNode;
                CompileTimeValue value;
                if (type.IsClassType() || type == _compilation.GetSpecialType(SpecialType.System_Void))
                {
                    value = new ConstantStaticValue(ConstantValue.Null);
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
                else if (MetaUtils.CheckIsSimpleStaticValueType(type, _compilation))
                {
                    if (type.SpecialType != SpecialType.None)
                    {
                        value = new ConstantStaticValue(ConstantValue.Default(type.SpecialType));
                    }
                    else
                    {
                        Debug.Assert(type.IsEnumType());
                        value = new EnumValue(type, ConstantValue.Default(type.GetEnumUnderlyingType().SpecialType));
                    }
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
                else
                {
                    rewrittenNode = MetaUtils.ConvertIfNeeded(
                        node.Type,
                        new BoundDefaultOperator(node.Syntax, type) { WasCompilerGenerated = true },
                        _compilation);
                    value = CompileTimeValue.Dynamic;
                }

                // A lone default expression should never be a stand-alone statement, so we return MustEmit = false
                return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        private DecorationRewriteResult VisitIsReadOnlyCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 1);
            CompileTimeValue propertyInfoValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (propertyInfoValue.Kind != CompileTimeValueKind.Dynamic)
            {
                if (propertyInfoValue is ConstantStaticValue)
                {
                    Debug.Assert(((ConstantStaticValue)propertyInfoValue).Value.IsNull);
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                    return new DecorationRewriteResult(
                        MakeBadExpression(syntax, node.Type),
                        variableValues,
                        true,
                        CompileTimeValue.Dynamic);
                }

                Debug.Assert(propertyInfoValue is PropertyInfoValue);
                PropertySymbol property = ((PropertyInfoValue)propertyInfoValue).Property;

                bool isReadOnly = (property.SetMethod == null);
                var value = new ConstantStaticValue(ConstantValue.Create(isReadOnly));
                return new DecorationRewriteResult(
                    MakeSimpleStaticValueExpression(value, node.Type, syntax),
                    variableValues,
                    false,
                    value);
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        private DecorationRewriteResult VisitIsWriteOnlyCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 1);
            CompileTimeValue propertyInfoValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (propertyInfoValue.Kind != CompileTimeValueKind.Dynamic)
            {
                if (propertyInfoValue is ConstantStaticValue)
                {
                    Debug.Assert(((ConstantStaticValue)propertyInfoValue).Value.IsNull);
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                    return new DecorationRewriteResult(
                        MakeBadExpression(syntax, node.Type),
                        variableValues,
                        true,
                        CompileTimeValue.Dynamic);
                }

                Debug.Assert(propertyInfoValue is PropertyInfoValue);
                PropertySymbol property = ((PropertyInfoValue)propertyInfoValue).Property;

                bool isReadOnly = (property.GetMethod == null);
                var value = new ConstantStaticValue(ConstantValue.Create(isReadOnly));
                return new DecorationRewriteResult(
                    MakeSimpleStaticValueExpression(value, node.Type, syntax),
                    variableValues,
                    false,
                    value);
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        private DecorationRewriteResult VisitParameterTypeCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 2);
            CompileTimeValue memberInfoValue = argumentsResults[0].Value;
            CompileTimeValue parameterIndexValue = argumentsResults[1].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (memberInfoValue.Kind != CompileTimeValueKind.Dynamic && parameterIndexValue.Kind != CompileTimeValueKind.Dynamic)
            {
                if (memberInfoValue is ConstantStaticValue)
                {
                    Debug.Assert(((ConstantStaticValue)memberInfoValue).Value.IsNull);
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                    return new DecorationRewriteResult(
                        MakeBadExpression(syntax, node.Type),
                        variableValues,
                        true,
                        CompileTimeValue.Dynamic);
                }

                Symbol member;
                if (memberInfoValue is MethodInfoValue)
                {
                    member = ((MethodInfoValue)memberInfoValue).Method;
                }
                else if (memberInfoValue is ConstructorInfoValue)
                {
                    member = ((ConstructorInfoValue)memberInfoValue).Constructor;
                }
                else
                {
                    Debug.Assert(memberInfoValue is PropertyInfoValue);
                    member = ((PropertyInfoValue)memberInfoValue).Property;
                }

                Debug.Assert(parameterIndexValue is ConstantStaticValue);
                ConstantValue parameterIndexConstantValue = ((ConstantStaticValue)parameterIndexValue).Value;
                Debug.Assert(parameterIndexConstantValue.SpecialType == SpecialType.System_Int32);
                int parameterIndex = parameterIndexConstantValue.Int32Value;

                int parameterCount;
                if (member.Kind == SymbolKind.Method)
                {
                    parameterCount = ((MethodSymbol)member).ParameterCount;
                }
                else
                {
                    Debug.Assert(member.Kind == SymbolKind.Property);
                    parameterCount = ((PropertySymbol)member).ParameterCount;
                }

                if (parameterIndex < 0 || parameterIndex >= parameterCount)
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticIndexOutOfBounds, syntax.Location);
                    return new DecorationRewriteResult(
                        MakeBadExpression(syntax, node.Type),
                        variableValues,
                        true,
                        CompileTimeValue.Dynamic);
                }
                else
                {
                    TypeSymbol parameterType;
                    if (member.Kind == SymbolKind.Method)
                    {
                        parameterType = ((MethodSymbol)member).ParameterTypes[parameterIndex];
                    }
                    else
                    {
                        Debug.Assert(member.Kind == SymbolKind.Property);
                        parameterType = ((PropertySymbol)member).ParameterTypes[parameterIndex];
                    }

                    var value = new TypeValue(parameterType);
                    return new DecorationRewriteResult(
                        MakeSimpleStaticValueExpression(value, node.Type, syntax),
                        variableValues,
                        false,
                        value);
                }
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        private DecorationRewriteResult VisitThisObjectTypeCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 1);
            CompileTimeValue memberInfoValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (memberInfoValue.Kind != CompileTimeValueKind.Dynamic)
            {
                if (memberInfoValue is ConstantStaticValue)
                {
                    Debug.Assert(((ConstantStaticValue)memberInfoValue).Value.IsNull);
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                    return new DecorationRewriteResult(
                        MakeBadExpression(syntax, node.Type),
                        variableValues,
                        true,
                        CompileTimeValue.Dynamic);
                }

                Symbol member;
                if (memberInfoValue is MethodInfoValue)
                {
                    member = ((MethodInfoValue)memberInfoValue).Method;
                }
                else if (memberInfoValue is ConstructorInfoValue)
                {
                    member = ((ConstructorInfoValue)memberInfoValue).Constructor;
                }
                else
                {
                    Debug.Assert(memberInfoValue is PropertyInfoValue);
                    member = ((PropertyInfoValue)memberInfoValue).Property;
                }

                TypeSymbol thisObjectType = member.IsStatic
                                                ? _compilation.GetSpecialType(SpecialType.System_Void)
                                                : member.ContainingType;
                var value = new TypeValue(thisObjectType);
                return new DecorationRewriteResult(
                    MakeSimpleStaticValueExpression(value, node.Type, syntax),
                    variableValues,
                    false,
                    value);
            }

            return new DecorationRewriteResult(
                node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                variableValues,
                true,
                CompileTimeValue.Dynamic);
        }

        private BoundExpression MakeAttributeCreationExpression(CSharpSyntaxNode syntax, CSharpAttributeData attribute, TypeSymbol type)
        {
            Debug.Assert(attribute != null);
            TypeSymbol attributeClass = attribute.AttributeClass;
            MethodSymbol attributeConstructor = attribute.AttributeConstructor;
            Debug.Assert(attributeClass != null && attributeConstructor != null);

            BoundExpression initializerExpression = null;
            if (attribute.NamedArguments != null && attribute.NamedArguments.Any())
            {
                // If the attribute declaration has one or more named arguments, we want to construct an object initializer that matches them
                ImmutableArray<BoundExpression>.Builder initializersBuilder = ImmutableArray.CreateBuilder<BoundExpression>();
                foreach (KeyValuePair<string, TypedConstant> kv in attribute.NamedArguments)
                {
                    ImmutableArray<Symbol> candidateSymbols = attributeClass.GetMembers(kv.Key);
                    Debug.Assert(!candidateSymbols.IsEmpty);

                    Symbol memberSymbol = candidateSymbols[0];
                    TypeSymbol memberType;
                    if (memberSymbol.Kind == SymbolKind.Field)
                    {
                        memberType = ((FieldSymbol)memberSymbol).Type;
                    }
                    else
                    {
                        Debug.Assert(memberSymbol.Kind == SymbolKind.Property);
                        memberType = ((PropertySymbol)memberSymbol).Type;
                    }

                    var initializerMember = new BoundObjectInitializerMember(
                        syntax,
                        memberSymbol,
                        ImmutableArray<BoundExpression>.Empty,
                        default(ImmutableArray<string>),
                        default(ImmutableArray<RefKind>),
                        false,
                        default(ImmutableArray<int>),
                        LookupResultKind.Viable,
                        memberType)
                    {
                        WasCompilerGenerated = true,
                    };

                    initializersBuilder.Add(
                        new BoundAssignmentOperator(
                            syntax,
                            initializerMember,
                            TypedConstantToExpression(syntax, kv.Value, memberType),
                            RefKind.None,
                            memberType)
                        {
                            WasCompilerGenerated = true,
                        });
                }

                initializerExpression = new BoundObjectInitializerExpression(
                    syntax,
                    initializersBuilder.ToImmutable(),
                    attributeClass)
                {
                    WasCompilerGenerated = true,
                };
            }

            ImmutableArray<BoundExpression>.Builder argumentsBuilder = ImmutableArray.CreateBuilder<BoundExpression>();
            int i = 0;
            foreach (TypedConstant argumentConstant in attribute.ConstructorArguments)
            {
                TypeSymbol argumentType = attributeConstructor.ParameterTypes[i];
                argumentsBuilder.Add(TypedConstantToExpression(syntax, argumentConstant, argumentType));
                i++;
            }

            var attributeCreationExpression = new BoundObjectCreationExpression(
                syntax,
                attribute.AttributeConstructor,
                argumentsBuilder.ToImmutable(),
                default(ImmutableArray<string>),
                default(ImmutableArray<RefKind>),
                false,
                default(ImmutableArray<int>),
                null,
                initializerExpression,
                attributeClass)
            {
                WasCompilerGenerated = true,
            };

            return MetaUtils.ConvertIfNeeded(type, attributeCreationExpression, _compilation);
        }

        private BoundExpression TypedConstantToExpression(CSharpSyntaxNode syntax, TypedConstant typedConstant, TypeSymbol type)
        {
            switch (typedConstant.Kind)
            {
                case TypedConstantKind.Error:
                    return MakeBadExpression(syntax, type);

                case TypedConstantKind.Primitive:
                case TypedConstantKind.Enum:
                    Debug.Assert(typedConstant.Type != null);
                    var constantValue = ConstantValue.Create(typedConstant.Value, typedConstant.Type.SpecialType);
                    return MetaUtils.ConvertIfNeeded(
                        type,
                        new BoundLiteral(syntax, constantValue, _compilation.GetSpecialType(constantValue.SpecialType)) { WasCompilerGenerated = true },
                        _compilation);

                case TypedConstantKind.Type:
                    Debug.Assert(typedConstant.Value is TypeSymbol);
                    return MetaUtils.ConvertIfNeeded(
                        type,
                        new BoundTypeOfOperator(
                            syntax,
                            new BoundTypeExpression(syntax, null, (TypeSymbol)typedConstant.Value) { WasCompilerGenerated = true },
                            null,
                            _compilation.GetWellKnownType(WellKnownType.System_Type))
                        {
                            WasCompilerGenerated = true,
                        },
                        _compilation);

                case TypedConstantKind.Array:
                    Debug.Assert(!typedConstant.Values.IsDefault && typedConstant.Type != null && typedConstant.Type is TypeSymbol);
                    var arrayType = (TypeSymbol)typedConstant.Type;
                    Debug.Assert(arrayType.IsArray());
                    TypeSymbol elementType = ((ArrayTypeSymbol)arrayType).ElementType;

                    int elementCount = typedConstant.Values.Length;
                    var elementExpressions = new BoundExpression[elementCount];
                    for (int i = 0; i < elementCount; i++)
                    {
                        elementExpressions[i] = TypedConstantToExpression(syntax, typedConstant.Values[i], elementType);
                    }

                    return MetaUtils.ConvertIfNeeded(
                        type,
                        new BoundArrayCreation(
                            syntax,
                            ImmutableArray.Create<BoundExpression>(
                                new BoundLiteral(syntax, ConstantValue.Create(elementCount), _compilation.GetSpecialType(SpecialType.System_Int32)) { WasCompilerGenerated = true }),
                            new BoundArrayInitialization(syntax, elementExpressions.ToImmutableArray()) { WasCompilerGenerated = true },
                            arrayType),
                        _compilation);

                default:
                    throw ExceptionUtilities.Unreachable;
            }
        }
    }
}
