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
                    rewrittenNode = new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type)
                    {
                        WasCompilerGenerated = true,
                    };
                    mustEmit = true;
                }
                else
                {
                    // Prepare a temporary variable for the result (replace void with object to maintain the semantics of reflection method invocation)
                    LocalSymbol resultLocal = method.ReturnsVoid
                                                ? null
                                                : _factory.SynthesizedLocal(_targetMethod.ReturnType, node.Syntax, kind: SynthesizedLocalKind.DecoratorTempResult);

                    // Prepare the spliced method body, which will be inserted prior to the current statement
                    SpliceMethodBody(resultLocal, node.Arguments[1], node.Syntax, variableValues);

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
                    new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type)
                    {
                        WasCompilerGenerated = true,
                    },
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
            if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAssignableFrom))
            {
                return VisitIsAssignableFromCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__GetParameters))
            {
                return VisitGetParametersCall(node, receiverResult, argumentsResults, variableValues);
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
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType))
            {
                return VisitParameterTypeCall(node, receiverResult, argumentsResults, variableValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType))
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
                    _diagnostics.Add(ErrorCode.ERR_InvalidInvokeInDecorator, call.Syntax.Location);
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

        private void SpliceMethodBody(LocalSymbol tempResultLocal, BoundExpression argumentsArray, CSharpSyntaxNode syntax, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            // All splice locations in the decorator method's body should be inside a block
            Debug.Assert(_blockLocalsBuilder != null);

            var postSpliceLabel = _factory.GenerateLabel($"decorator_{_decoratorOrdinal}_post_splice_{_spliceOrdinal}");
            int parameterCount = _targetMethod.ParameterCount;

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

            var parameterReplacementsBuilder = ImmutableDictionary.CreateBuilder<Symbol, Symbol>();
            LocalSymbol[] parameterReplacementLocals = null;
            BoundLocal argumentsArrayLocalNode = null;
            if (argumentsArrayValue.Kind == CompileTimeValueKind.ArgumentArray)
            {
                var staticArgumentsArray = (ArgumentArrayValue)argumentsArrayValue;

                // Prepare parameter replacements dictionary
                for (int i = 0; i < parameterCount; i++)
                {
                    parameterReplacementsBuilder.Add(_targetMethod.Parameters[i], staticArgumentsArray.ArgumentSymbols[i]);
                }
            }
            else
            {
                Debug.Assert(argumentsArrayValue.Kind == CompileTimeValueKind.Dynamic);
                LocalSymbol argumentsArrayLocal = GetReplacementSymbol(argumentsArraySymbol);
                argumentsArrayLocalNode = new BoundLocal(syntax, argumentsArrayLocal, null, argumentsArrayLocal.Type) { WasCompilerGenerated = true };

                // Declare fresh variables that will replace the spliced method body's parameters and generate assignments for them
                parameterReplacementLocals = new LocalSymbol[parameterCount];
                for (int i = 0; i < parameterCount; i++)
                {
                    ParameterSymbol parameter = _targetMethod.Parameters[i];
                    LocalSymbol parameterReplacementLocal = _factory.SynthesizedLocal(
                        parameter.Type,
                        syntax: syntax,
                        kind: SynthesizedLocalKind.DecoratedMethodParameter);
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
                _diagnostics);
            _splicedStatementsBuilder.Add(spliceRewrittenBody);

            // Insert the post-splice label
            _splicedStatementsBuilder.Add(new BoundLabelStatement(syntax, postSpliceLabel) { WasCompilerGenerated = true });

            // If temporary replacement variables were created for the spliced method body's parameters, we copy their final values back to the dynamic argument array
            if (parameterReplacementLocals != null)
            {
                for (int i = 0; i < parameterCount; i++)
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

        private DecorationRewriteResult VisitIsAssignableFromCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(receiverResult != null && argumentsResults.Length == 1);
            CompileTimeValue receiverValue = receiverResult.Value;
            CompileTimeValue otherTypeValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type)
                    {
                        WasCompilerGenerated = true,
                    },
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            if (otherTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)otherTypeValue).Value.IsNull);
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                return new DecorationRewriteResult(
                    new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type)
                    {
                        WasCompilerGenerated = true,
                    },
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            if (receiverValue.Kind == CompileTimeValueKind.Dynamic || otherTypeValue.Kind == CompileTimeValueKind.Dynamic)
            {
                return new DecorationRewriteResult(
                    node.Update((BoundExpression)receiverResult.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node)),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            Debug.Assert(receiverValue is TypeValue && otherTypeValue is TypeValue);
            var value = new ConstantStaticValue(
                ConstantValue.Create(
                    MetaUtils.CheckTypeIsAssignableFrom(((TypeValue)receiverValue).Type, ((TypeValue)otherTypeValue).Type)));
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
                    MakeBadExpression(node.Syntax, node.Type),
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            if (receiverValue.Kind == CompileTimeValueKind.Complex)
            {
                Debug.Assert(receiverValue is MethodInfoValue);
                MethodSymbol method = ((MethodInfoValue)receiverValue).Method;
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
                _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                return new DecorationRewriteResult(
                    new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type)
                    {
                        WasCompilerGenerated = true,
                    },
                    variableValues,
                    true,
                    CompileTimeValue.Dynamic);
            }

            CompileTimeValue value;
            BoundExpression rewrittenNode;
            MethodSymbol invokedMethod = node.Method;
            TypeSymbol requestedAttributeType = invokedMethod.TypeArguments[0];
            if (argumentValue.Kind == CompileTimeValueKind.Dynamic)
            {
                value = CompileTimeValue.Dynamic;
                rewrittenNode = node.Update((BoundExpression)receiverResult?.Node, node.Method, argumentsResults.SelectAsArray(r => (BoundExpression)r.Node));
            }
            else if (argumentValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(argumentValue is TypeValue
                             && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                TypeSymbol type = ((TypeValue)argumentValue).Type;
                LookupCustomAttributeValue(node.Syntax, requestedAttributeType, type.GetAttributes(), out value, out rewrittenNode);
            }
            else
            {
                Debug.Assert(argumentValue.Kind == CompileTimeValueKind.Complex);
                if (argumentValue is MethodInfoValue)
                {
                    Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                    MethodSymbol method = ((MethodInfoValue)argumentValue).Method;
                    LookupCustomAttributeValue(node.Syntax, requestedAttributeType, method.GetAttributes(), out value, out rewrittenNode);
                }
                else
                {
                    Debug.Assert(argumentValue is ParameterInfoValue
                                 && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2));
                    ParameterSymbol parameter = ((ParameterInfoValue)argumentValue).Parameter;
                    LookupCustomAttributeValue(node.Syntax, requestedAttributeType, parameter.GetAttributes(), out value, out rewrittenNode);
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
            int parameterCount = _targetMethod.ParameterCount;

            if (_flags.HasFlag(DecorationRewriterFlags.ExpectedDynamicArgumentArray))
            {
                var boundsExpression = new BoundLiteral(
                    syntax,
                    ConstantValue.Create(parameterCount),
                    _compilation.GetSpecialType(SpecialType.System_Int32))
                {
                    WasCompilerGenerated = true,
                };

                BoundArrayInitialization arrayInitialization = null;
                if (parameterCount > 0)
                {
                    var arrayInitializationExpressions = new BoundExpression[parameterCount];

                    if (argumentArrayValue.Kind == CompileTimeValueKind.ArgumentArray)
                    {
                        Debug.Assert(argumentArrayValue is ArgumentArrayValue);
                        ImmutableArray<Symbol> argumentSymbols = ((ArgumentArrayValue)argumentArrayValue).ArgumentSymbols;
                        for (int i = 0; i < parameterCount; i++)
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
                        for (int i = 0; i < parameterCount; i++)
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
                var newArgumentLocals = new LocalSymbol[parameterCount];
                for (int i = 0; i < parameterCount; i++)
                {
                    TypeSymbol parameterType = _targetMethod.ParameterTypes[i];
                    LocalSymbol newArgumentLocal = _factory.SynthesizedLocal(
                        parameterType,
                        syntax: syntax,
                        kind: SynthesizedLocalKind.DecoratedMethodParameter);
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
                    new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type),
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
            int parameterCount = _targetMethod.ParameterCount;

            var boundsExpression = new BoundLiteral(
                syntax,
                ConstantValue.Create(parameterCount),
                _compilation.GetSpecialType(SpecialType.System_Int32))
            {
                WasCompilerGenerated = true,
            };

            BoundArrayInitialization arrayInitialization = null;
            if (parameterCount > 0)
            {
                var arrayInitializationExpressions = new BoundExpression[parameterCount];

                if (argumentArrayValue.Kind == CompileTimeValueKind.ArgumentArray)
                {
                    Debug.Assert(argumentArrayValue is ArgumentArrayValue);
                    ImmutableArray<Symbol> argumentSymbols = ((ArgumentArrayValue)argumentArrayValue).ArgumentSymbols;
                    for (int i = 0; i < parameterCount; i++)
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
                    for (int i = 0; i < parameterCount; i++)
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

        private DecorationRewriteResult VisitParameterTypeCall(
            BoundCall node,
            DecorationRewriteResult receiverResult,
            ImmutableArray<DecorationRewriteResult> argumentsResults,
            ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            Debug.Assert(argumentsResults.Length == 2);
            CompileTimeValue methodInfoValue = argumentsResults[0].Value;
            CompileTimeValue parameterIndexValue = argumentsResults[1].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (methodInfoValue.Kind != CompileTimeValueKind.Dynamic && parameterIndexValue.Kind != CompileTimeValueKind.Dynamic)
            {
                Debug.Assert(methodInfoValue is MethodInfoValue && parameterIndexValue is ConstantStaticValue);
                MethodSymbol method = ((MethodInfoValue)methodInfoValue).Method;
                ConstantValue parameterIndexConstantValue = ((ConstantStaticValue)parameterIndexValue).Value;
                Debug.Assert(parameterIndexConstantValue.SpecialType == SpecialType.System_Int32);
                int parameterIndex = parameterIndexConstantValue.Int32Value;

                if (parameterIndex < 0 || parameterIndex >= method.ParameterCount)
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
                    var value = new TypeValue(method.ParameterTypes[parameterIndex]);
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
            CompileTimeValue methodInfoValue = argumentsResults[0].Value;
            CSharpSyntaxNode syntax = node.Syntax;

            if (methodInfoValue.Kind != CompileTimeValueKind.Dynamic)
            {
                Debug.Assert(methodInfoValue is MethodInfoValue);
                MethodSymbol method = ((MethodInfoValue)methodInfoValue).Method;

                TypeSymbol thisObjectType = method.IsStatic
                                                ? _compilation.GetSpecialType(SpecialType.System_Void)
                                                : method.ContainingType;
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

        private void LookupCustomAttributeValue(
            CSharpSyntaxNode syntax,
            TypeSymbol attributeType,
            ImmutableArray<CSharpAttributeData> attributes,
            out CompileTimeValue value,
            out BoundExpression rewrittenNode)
        {
            if (attributes.IsDefaultOrEmpty)
            {
                value = new ConstantStaticValue(ConstantValue.Null);
                rewrittenNode = MakeSimpleStaticValueExpression(value, attributeType, syntax);
                return;
            }

            var candidateAttributes = new List<CSharpAttributeData>();
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            foreach (CSharpAttributeData attribute in attributes)
            {
                if (attribute.AttributeClass.IsEqualToOrDerivedFrom(attributeType, false, ref useSiteDiagnostics))
                {
                    candidateAttributes.Add(attribute);
                }
            }

            switch (candidateAttributes.Count)
            {
                case 0:
                    value = new ConstantStaticValue(ConstantValue.Null);
                    rewrittenNode = MakeSimpleStaticValueExpression(value, attributeType, syntax);
                    break;

                case 1:
                    value = new AttributeValue(candidateAttributes[0]);
                    rewrittenNode = MakeAttributeCreationExpression(syntax, candidateAttributes[0], attributeType);
                    break;

                default:
                    _diagnostics.Add(ErrorCode.ERR_StaticAmbiguousMatch, syntax.Location);
                    value = CompileTimeValue.Dynamic;
                    rewrittenNode = MakeBadExpression(syntax, attributeType);
                    break;
            }
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
