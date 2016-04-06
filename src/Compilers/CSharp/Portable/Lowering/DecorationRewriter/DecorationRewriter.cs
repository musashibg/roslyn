using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class DecorationRewriter : BoundTreeRewriterWithStackGuard
    {
        private readonly CSharpCompilation _compilation;
        private readonly SyntheticBoundNodeFactory _factory;
        private readonly BoundBlock _targetBody;
        private readonly SourceMemberMethodSymbol _decoratorMethod;
        private readonly int _decoratorOrdinal;
        private readonly DiagnosticBag _diagnostics;
        private readonly Dictionary<Symbol, LocalSymbol> _replacementSymbols;

        private DecorationRewriterFlags _flags;
        private ArrayBuilder<BoundStatement> _splicedStatements;
        private ArrayBuilder<LocalSymbol> _blockLocals;
        private int _spliceOrdinal;

        public DecorationRewriter(
            CSharpCompilation compilation,
            MethodSymbol containingMethod,
            BoundBlock targetBody,
            SourceMemberMethodSymbol decoratorMethod,
            int decoratorOrdinal,
            SyntheticBoundNodeFactory factory,
            DiagnosticBag diagnostics)
        {
            _compilation = compilation;
            _factory = factory;
            _factory.CurrentMethod = containingMethod;
            _targetBody = targetBody;
            _decoratorMethod = decoratorMethod;
            _decoratorOrdinal = decoratorOrdinal;
            Debug.Assert(factory.CurrentType == containingMethod.ContainingType);
            _diagnostics = diagnostics;
            _replacementSymbols = new Dictionary<Symbol, LocalSymbol>();

            _flags = DecorationRewriterFlags.None;
            _spliceOrdinal = 0;
        }

        public static BoundBlock Rewrite(
            CSharpCompilation compilation,
            MethodSymbol method,
            BoundBlock methodBody,
            DecoratorData decoratorData,
            int decoratorOrdinal,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)
        {
            Debug.Assert(methodBody != null);
            Debug.Assert(compilationState != null);
            Debug.Assert(decoratorData != null);

            try
            {
                SourceMemberMethodSymbol decoratorMethod = GetDecoratorMethod(compilation, method, decoratorData, compilationState, diagnostics);
                if (decoratorMethod == null)
                {
                    return methodBody;
                }

                BoundStatement decoratorBody = decoratorMethod.EarlyBoundBody;
                if (decoratorBody == null)
                {
                    diagnostics.Add(ErrorCode.ERR_DecoratorMethodWithoutBody, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorMethod.ContainingType);
                    return methodBody;
                }
                else if (decoratorBody.HasAnyErrors)
                {
                    return methodBody;
                }

                // Decorate target method body
                CSharpSyntaxNode methodSyntax = methodBody.Syntax;
                var factory = new SyntheticBoundNodeFactory(method, methodSyntax, compilationState, diagnostics);
                var decorationRewriter = new DecorationRewriter(compilation, method, methodBody, decoratorMethod, decoratorOrdinal, factory, diagnostics);
                var decoratedBody = (BoundBlock)decorationRewriter.Visit(decoratorBody);

                // Generate the decoration prologue
                var prologueStatements = new ArrayBuilder<BoundStatement>();
                var prologueLocals = new ArrayBuilder<LocalSymbol>();
                if (decorationRewriter.HasEncounteredParameterSymbol(0))
                {
                    // Assign the target method's info to the corresponding decorator method parameter
                    LocalSymbol methodLocal = decorationRewriter.GetReplacementSymbol(decoratorMethod.Parameters[0], methodSyntax);
                    prologueLocals.Add(methodLocal);
                    BoundExpression methodInfoExpression = factory.MethodInfo(method);

                    prologueStatements.Add(
                        new BoundLocalDeclaration(
                            methodSyntax,
                            methodLocal,
                            new BoundTypeExpression(methodSyntax, null, methodLocal.Type),
                            methodInfoExpression,
                            default(ImmutableArray<BoundExpression>)));
                }
                if (decorationRewriter.HasEncounteredParameterSymbol(2))
                {
                    // Assign an array containing all target method arguments to the corresponding decorator method parameter
                    LocalSymbol argumentsLocal = decorationRewriter.GetReplacementSymbol(decoratorMethod.Parameters[2], methodSyntax);
                    prologueLocals.Add(argumentsLocal);
                    int parameterCount = method.ParameterCount;

                    var boundsExpression = new BoundLiteral(
                        methodSyntax,
                        ConstantValue.Create(parameterCount, SpecialType.System_Int32),
                        compilation.GetSpecialType(SpecialType.System_Int32));

                    TypeSymbol objectType = compilation.GetSpecialType(SpecialType.System_Object);
                    BoundArrayInitialization arrayInitialization = null;
                    if (parameterCount > 0)
                    {
                        var arrayInitializationExpressions = new BoundExpression[parameterCount];
                        for (int i = 0; i < parameterCount; i++)
                        {
                            arrayInitializationExpressions[i] = factory.Convert(
                                objectType,
                                new BoundParameter(methodSyntax, method.Parameters[i]));
                        }
                        arrayInitialization = new BoundArrayInitialization(methodSyntax, arrayInitializationExpressions.ToImmutableArray());
                    }

                    var argumentsArrayExpression = new BoundArrayCreation(
                        methodSyntax,
                        ImmutableArray.Create<BoundExpression>(boundsExpression),
                        arrayInitialization,
                        compilation.CreateArrayTypeSymbol(objectType));

                    prologueStatements.Add(
                        new BoundLocalDeclaration(
                            methodSyntax,
                            argumentsLocal,
                            new BoundTypeExpression(methodSyntax, null, argumentsLocal.Type),
                            argumentsArrayExpression,
                            default(ImmutableArray<BoundExpression>)));
                }

                // If a prologue was generated, prepend it to the decorated body
                if (prologueStatements.Count > 0)
                {
                    ImmutableArray<BoundStatement> newStatements =
                        prologueStatements.ToImmutableAndFree()
                        .AddRange(decoratedBody.Statements);
                    ImmutableArray<LocalSymbol> newLocals =
                        prologueLocals.ToImmutableAndFree()
                        .AddRange(decoratedBody.Locals);
                    decoratedBody = decoratedBody.Update(newLocals, newStatements);
                }

                return decoratedBody;
            }
            catch (SyntheticBoundNodeFactory.MissingPredefinedMember ex)
            {
                diagnostics.Add(ex.Diagnostic);
                return new BoundBlock(methodBody.Syntax, default(ImmutableArray<LocalSymbol>), ImmutableArray.Create<BoundStatement>(methodBody), hasErrors: true);
            }
        }

        public override BoundNode Visit(BoundNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node is BoundStatement)
            {
                ArrayBuilder<BoundStatement> outerSplicedStatements = _splicedStatements;

                _splicedStatements = new ArrayBuilder<BoundStatement>();
                BoundNode rewrittenNode = base.Visit(node);
                if (_splicedStatements.Count == 0)
                {
                    _splicedStatements.Free();

                    _splicedStatements = outerSplicedStatements;

                    return rewrittenNode;
                }

                if (rewrittenNode is BoundStatement)
                {
                    _splicedStatements.Add((BoundStatement)rewrittenNode);
                }
                else if (rewrittenNode is BoundStatementList)
                {
                    _splicedStatements.AddRange(((BoundStatementList)rewrittenNode).Statements);
                }
                ImmutableArray<BoundStatement> statements = _splicedStatements.ToImmutableAndFree();

                _splicedStatements = outerSplicedStatements;

                return new BoundStatementList(node.Syntax, statements);
            }
            else
            {
                return base.Visit(node);
            }
        }

        public override BoundNode VisitBaseReference(BoundBaseReference node)
        {
            // References to the decorator object would be meaningless in the target method's context
            // Report an error and replace with a null literal just to preserve type safety
            _diagnostics.Add(ErrorCode.ERR_ThisReferenceInDecorator, node.Syntax.Location);
            return new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type);
        }

        public override BoundNode VisitBlock(BoundBlock node)
        {
            ArrayBuilder<LocalSymbol> outerBlockLocals = _blockLocals;
            _blockLocals = new ArrayBuilder<LocalSymbol>();

            ImmutableArray<BoundStatement> statements = VisitList(node.Statements);

            ImmutableArray<LocalSymbol> locals = _blockLocals.ToImmutableAndFree();
            _blockLocals = outerBlockLocals;

            return node.Update(locals, statements);
        }

        public override BoundNode VisitCall(BoundCall node)
        {
            if (IsSpliceLocation(node))
            {
                if (_flags.HasFlag(DecorationRewriterFlags.ProhibitSpliceLocation))
                {
                    _diagnostics.Add(ErrorCode.ERR_InvalidSpliceLocation, node.Syntax.Location);
                    return new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type);
                }

                if (_spliceOrdinal > 0)
                {
                    _diagnostics.Add(ErrorCode.ERR_MultipleInvokesInDecorator, node.Syntax.Location);
                    return new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type);
                }
                
                // Prepare a temporary variable for the result (replace void with object to maintain the semantics of reflection method invocation)
                MethodSymbol method = node.Method;
                LocalSymbol resultLocal = method.ReturnsVoid
                                            ? null
                                            : _factory.SynthesizedLocal(method.ReturnType, node.Syntax, kind: SynthesizedLocalKind.DecoratorTempResult);

                // Prepare the spliced method body, which will be inserted prior to the current statement
                var argumentsArray = (BoundLocal)Visit(node.Arguments[1]);
                SpliceMethodBody(resultLocal, argumentsArray, node.Syntax);

                // Replace the method invocation call with a reference to the result variable
                if (resultLocal == null)
                {
                    return new BoundLiteral(node.Syntax, ConstantValue.Null, node.Type);
                }
                else
                {
                    return new BoundLocal(node.Syntax, resultLocal, null, resultLocal.Type);
                }
            }

            if (IsBaseDecoratorMethodCall(node))
            {
                _diagnostics.Add(ErrorCode.ERR_BaseDecoratorMethodCallNotSupported, node.Syntax.Location);
                return new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type);
            }

            // If no splicing was performed, process the call expression as all others
            var receiverOpt = (BoundExpression)Visit(node.ReceiverOpt);
            ImmutableArray<BoundExpression> arguments = VisitList(node.Arguments);
            return node.Update(
                receiverOpt,
                node.Method,
                arguments,
                node.ArgumentNamesOpt,
                node.ArgumentRefKindsOpt,
                node.IsDelegateCall,
                node.Expanded,
                node.InvokedAsExtensionMethod,
                node.ArgsToParamsOpt,
                node.ResultKind,
                node.Type);
        }

        public override BoundNode VisitCatchBlock(BoundCatchBlock node)
        {
            LocalSymbol localOpt = node.LocalOpt == null
                                    ? null
                                    : GetReplacementSymbol(node.LocalOpt, node.Syntax);
            var exceptionSourceOpt = (BoundExpression)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.ExceptionSourceOpt);
            var exceptionFilterOpt = (BoundExpression)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.ExceptionFilterOpt);
            var body = (BoundBlock)VisitBlock(node.Body);
            return node.Update(localOpt, exceptionSourceOpt, node.ExceptionTypeOpt, exceptionFilterOpt, body, node.IsSynthesizedAsyncCatchAll);
        }

        public override BoundNode VisitDoStatement(BoundDoStatement node)
        {
            var condition = (BoundExpression)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition);
            var body = (BoundStatement)Visit(node.Body);
            return node.Update(condition, body, node.BreakLabel, node.ContinueLabel);
        }

        public override BoundNode VisitForEachStatement(BoundForEachStatement node)
        {
            LocalSymbol iterationVariable = GetReplacementSymbol(node.IterationVariable, node.Syntax);
            var expression = (BoundExpression)Visit(node.Expression);
            var body = (BoundStatement)Visit(node.Body);
            return node.Update(
                node.EnumeratorInfoOpt,
                node.ElementConversion,
                node.IterationVariableType,
                iterationVariable,
                expression,
                body,
                node.Checked,
                node.BreakLabel,
                node.ContinueLabel);
        }

        public override BoundNode VisitForStatement(BoundForStatement node)
        {
            var initializer = (BoundStatement)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Initializer);
            var condition = (BoundExpression)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition);
            var increment = (BoundStatement)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Increment);
            var body = (BoundStatement)Visit(node.Body);
            return node.Update(node.OuterLocals, initializer, condition, increment, body, node.BreakLabel, node.ContinueLabel);
        }

        public override BoundNode VisitLambda(BoundLambda node)
        {
            var body = (BoundBlock)VisitBlock(node.Body);
            return node.Update(node.Symbol, body, node.Diagnostics, node.Binder, node.Type);
        }

        public override BoundNode VisitLocal(BoundLocal node)
        {
            LocalSymbol localSymbol = GetReplacementSymbol(node.LocalSymbol, node.Syntax);
            return node.Update(localSymbol, node.ConstantValueOpt, node.Type);
        }

        public override BoundNode VisitLocalDeclaration(BoundLocalDeclaration node)
        {
            // All local variable declarations in the decorator method's body should be inside a block
            Debug.Assert(_blockLocals != null);

            LocalSymbol localSymbol = GetReplacementSymbol(node.LocalSymbol, node.Syntax);
            _blockLocals.Add(localSymbol);

            var initializerOpt = (BoundExpression)Visit(node.InitializerOpt);
            ImmutableArray<BoundExpression> argumentsOpt = VisitList(node.ArgumentsOpt);
            return node.Update(localSymbol, node.DeclaredType, initializerOpt, argumentsOpt);
        }

        public override BoundNode VisitParameter(BoundParameter node)
        {
            Debug.Assert(_decoratorMethod.ParameterCount == 3);

            var parameter = node.ParameterSymbol;
            if (parameter == _decoratorMethod.Parameters[1])
            {
                // Replace the object parameter of the decorator with a this reference in the decorated method
                return new BoundThisReference(node.Syntax, node.Type);
            }
            else if (_decoratorMethod.Parameters.Contains(parameter))
            {
                // Replace the parameter from the decorator with a local
                LocalSymbol localSymbol = GetReplacementSymbol(node.ParameterSymbol, node.Syntax);
                return new BoundLocal(node.Syntax, localSymbol, null, node.Type);
            }
            else
            {
                // This must be a lambda parameter - keep it intact
                return node;
            }
        }

        public override BoundNode VisitReturnStatement(BoundReturnStatement node)
        {
            var expressionOpt = (BoundExpression)Visit(node.ExpressionOpt);

            Debug.Assert(expressionOpt != null);

            if (_factory.CurrentMethod.ReturnsVoid)
            {
                // Assign expression to dummy local to preserve side effects
                CSharpSyntaxNode syntax = node.Syntax;
                LocalSymbol dummyLocal = _factory.SynthesizedLocal(expressionOpt.Type, syntax, kind: SynthesizedLocalKind.DecoratorTempLocal);

                var statements = new BoundStatement[2];
                statements[0] = new BoundLocalDeclaration(
                    syntax,
                    dummyLocal,
                    new BoundTypeExpression(syntax, null, dummyLocal.Type),
                    expressionOpt,
                    default(ImmutableArray<BoundExpression>));
                statements[1] = node.Update(null);

                return new BoundBlock(
                    syntax,
                    ImmutableArray.Create<LocalSymbol>(dummyLocal),
                    statements.ToImmutableArray());
            }
            else
            {
                return node.Update(_factory.Convert(_factory.CurrentMethod.ReturnType, expressionOpt));
            }
        }

        public override BoundNode VisitThisReference(BoundThisReference node)
        {
            // References to the decorator object would be meaningless in the target method's context
            // Report an error and replace with a null literal just to preserve type safety
            _diagnostics.Add(ErrorCode.ERR_ThisReferenceInDecorator, node.Syntax.Location);
            return new BoundBadExpression(node.Syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, node.Type);
        }

        public override BoundNode VisitUsingStatement(BoundUsingStatement node)
        {
            var declarationsOpt = (BoundMultipleLocalDeclarations)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.DeclarationsOpt);
            var expressionOpt = (BoundExpression)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.ExpressionOpt);
            var body = (BoundStatement)Visit(node.Body);
            return node.Update(node.Locals, declarationsOpt, expressionOpt, node.IDisposableConversion, body);
        }

        public override BoundNode VisitWhileStatement(BoundWhileStatement node)
        {
            var condition = (BoundExpression)VisitWithExtraFlags(DecorationRewriterFlags.ProhibitSpliceLocation, node.Condition);
            var body = (BoundStatement)Visit(node.Body);
            return node.Update(condition, body, node.BreakLabel, node.ContinueLabel);
        }

        public override BoundNode VisitYieldBreakStatement(BoundYieldBreakStatement node)
        {
            _diagnostics.Add(ErrorCode.ERR_BadYieldInDecoratorMethod, node.Syntax.Location);
            return new BoundBadStatement(node.Syntax, ImmutableArray<BoundNode>.Empty);
        }

        public override BoundNode VisitYieldReturnStatement(BoundYieldReturnStatement node)
        {
            _diagnostics.Add(ErrorCode.ERR_BadYieldInDecoratorMethod, node.Syntax.Location);
            return new BoundBadStatement(node.Syntax, ImmutableArray<BoundNode>.Empty);
        }

        public BoundNode VisitWithExtraFlags(DecorationRewriterFlags extraFlags, BoundNode node)
        {
            DecorationRewriterFlags oldFlags = _flags;
            _flags |= extraFlags;

            BoundNode resultNode = Visit(node);

            _flags = oldFlags;

            return resultNode;
        }

        private static SourceMemberMethodSymbol GetDecoratorMethod(
            CSharpCompilation compilation,
            MethodSymbol targetMethod,
            DecoratorData decoratorData,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)
        {
            var decoratorClass = decoratorData.DecoratorClass as SourceNamedTypeSymbol;
            if (decoratorClass == null)
            {
                diagnostics.Add(ErrorCode.ERR_NonSourceDecoratorClass, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorClass);
                return null;
            }

            SourceMemberMethodSymbol decoratorMethod = decoratorClass.FindDecoratorMethod();
            while (decoratorMethod == null)
            {
                decoratorClass = decoratorClass.BaseType as SourceNamedTypeSymbol;
                if (decoratorData == null)
                {
                    diagnostics.Add(ErrorCode.ERR_DecoratorDoesNotSupportMethods, decoratorData.ApplicationSyntaxReference.GetLocation(), decoratorClass, targetMethod);
                    return null;
                }
                decoratorMethod = decoratorClass.FindDecoratorMethod();
            }
            return decoratorMethod;
        }

        private static bool CheckIsSpecificParameter(BoundExpression node, ParameterSymbol parameter)
        {
            while (node.Kind == BoundKind.Conversion)
            {
                node = ((BoundConversion)node).Operand;
            }
            return node.Kind == BoundKind.Parameter && ((BoundParameter)node).ParameterSymbol == parameter;
        }

        private LocalSymbol GetReplacementSymbol(Symbol originalSymbol, CSharpSyntaxNode syntax)
        {
            LocalSymbol replacementSymbol = null;
            if (!_replacementSymbols.TryGetValue(originalSymbol, out replacementSymbol))
            {
                var parameter = originalSymbol as ParameterSymbol;
                if (parameter != null)
                {
                    replacementSymbol = _factory.SynthesizedLocal(
                        parameter.Type,
                        syntax: syntax,
                        kind: SynthesizedLocalKind.DecoratorParameter);
                }
                else
                {
                    var local = originalSymbol as LocalSymbol;
                    if (local != null)
                    {
                        replacementSymbol = _factory.SynthesizedLocal(
                            local.Type,
                            syntax: syntax,
                            kind: SynthesizedLocalKind.DecoratorLocal);
                    }
                }
                Debug.Assert(replacementSymbol != null);
                _replacementSymbols[originalSymbol] = replacementSymbol;
            }
            return replacementSymbol;
        }

        private bool HasEncounteredParameterSymbol(int parameterIndex)
        {
            return _replacementSymbols.ContainsKey(_decoratorMethod.Parameters[parameterIndex]);
        }

        private bool IsSpliceLocation(BoundCall call)
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

        private bool IsBaseDecoratorMethodCall(BoundCall call)
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

        private void SpliceMethodBody(LocalSymbol tempResultLocal, BoundLocal argumentsArray, CSharpSyntaxNode syntax)
        {
            // All splice locations in the decorator method's body should be inside a block
            Debug.Assert(_blockLocals != null);

            var postSpliceLabel = _factory.GenerateLabel($"decorator_{_decoratorOrdinal}_post_splice_{_spliceOrdinal}");

            // Assign the invocation arguments to the corresponding target method parameters
            int parameterCount = _factory.CurrentMethod.ParameterCount;
            for (int i = 0; i < parameterCount; i++)
            {
                ParameterSymbol parameter = _factory.CurrentMethod.Parameters[i];
                _splicedStatements.Add(new BoundExpressionStatement(
                    syntax,
                    new BoundAssignmentOperator(
                        syntax,
                        new BoundParameter(syntax, parameter),
                        _factory.Convert(
                            parameter.Type,
                            new BoundArrayAccess(
                                syntax,
                                argumentsArray,
                                ImmutableArray.Create<BoundExpression>(
                                    new BoundLiteral(syntax, ConstantValue.Create(i, SpecialType.System_Int32), _compilation.GetSpecialType(SpecialType.System_Int32))),
                                _compilation.GetSpecialType(SpecialType.System_Object))),
                        RefKind.None,
                        parameter.Type)));
            }

            // Insert temp result local variable declaration
            if (tempResultLocal != null)
            {
                _blockLocals.Add(tempResultLocal);
                _splicedStatements.Add(
                    new BoundLocalDeclaration(
                        syntax,
                        tempResultLocal,
                        new BoundTypeExpression(syntax, null, tempResultLocal.Type),
                        null,
                        default(ImmutableArray<BoundExpression>)));
            }

            // Insert the spliced method body
            var spliceRewrittenBody = SplicedMethodBodyRewriter.Rewrite(_factory, tempResultLocal, postSpliceLabel, _spliceOrdinal, _targetBody, _diagnostics);
            _splicedStatements.Add(spliceRewrittenBody);

            // Insert the post-splice label
            _splicedStatements.Add(new BoundLabelStatement(syntax, postSpliceLabel));

            _spliceOrdinal++;
        }
    }
}
