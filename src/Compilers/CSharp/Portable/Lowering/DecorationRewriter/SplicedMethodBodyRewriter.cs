using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class SplicedMethodBodyRewriter : BoundTreeRewriterWithStackGuard
    {
        private readonly CSharpCompilation _compilation;
        private readonly SyntheticBoundNodeFactory _factory;
        private readonly LocalSymbol _tempResultLocal;
        private readonly LabelSymbol _postSpliceLabel;
        private readonly int _spliceOrdinal;
        private readonly VariableNameGenerator _variableNamesGenerator;
        private readonly DiagnosticBag _diagnostics;

        private SplicedMethodBodyRewriterFlags _flags;
        private ImmutableDictionary<Symbol, Symbol> _replacementSymbols;
        private ImmutableArray<LocalSymbol>.Builder _blockLocalsBuilder;

        public SplicedMethodBodyRewriter(
            SyntheticBoundNodeFactory factory,
            LocalSymbol tempResultLocal,
            LabelSymbol postSpliceLabel,
            int spliceOrdinal,
            ImmutableDictionary<Symbol, Symbol> initialReplacementSymbols,
            VariableNameGenerator variableNamesGenerator,
            DiagnosticBag diagnostics)
        {
            _compilation = factory.Compilation;
            _factory = factory;
            _tempResultLocal = tempResultLocal;
            _postSpliceLabel = postSpliceLabel;
            _spliceOrdinal = spliceOrdinal;
            _replacementSymbols = initialReplacementSymbols;
            _variableNamesGenerator = variableNamesGenerator;
            _diagnostics = diagnostics;
            _blockLocalsBuilder = null;
        }

        public static BoundBlock Rewrite(
            SyntheticBoundNodeFactory factory,
            LocalSymbol tempResultLocal,
            LabelSymbol postSpliceLabel,
            int spliceOrdinal,
            BoundBlock targetBody,
            ImmutableDictionary<Symbol, Symbol> initialReplacementSymbols,
            VariableNameGenerator variableNamesGenerator,
            DiagnosticBag diagnostics)
        {
            var splicedMethodBodyRewriter = new SplicedMethodBodyRewriter(
                factory,
                tempResultLocal,
                postSpliceLabel,
                spliceOrdinal,
                initialReplacementSymbols,
                variableNamesGenerator,
                diagnostics);
            return (BoundBlock)splicedMethodBodyRewriter.VisitBlock(targetBody);
        }

        public override BoundNode VisitBlock(BoundBlock node)
        {
            Debug.Assert(node.LocalFunctions.IsEmpty, "SplicedMethodBodyRewriter is not compatible with local functions.");
            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            ImmutableArray<BoundStatement> statements = VisitList(node.Statements);

            ImmutableArray<LocalSymbol> locals = _blockLocalsBuilder.ToImmutable();
            _blockLocalsBuilder = outerBlockLocalsBuilder;

            return node.Update(locals, node.LocalFunctions, FlattenStatementList(statements));
        }

        public override BoundNode VisitCatchBlock(BoundCatchBlock node)
        {
            ImmutableArray<LocalSymbol> locals = node.Locals.SelectAsArray(ls => (LocalSymbol)GetReplacementSymbol(ls));
            var exceptionSourceOpt = (BoundExpression)Visit(node.ExceptionSourceOpt);
            var exceptionFilterOpt = (BoundExpression)Visit(node.ExceptionFilterOpt);
            var body = (BoundBlock)Visit(node.Body);
            return node.Update(locals, exceptionSourceOpt, node.ExceptionTypeOpt, exceptionFilterOpt, body, node.IsSynthesizedAsyncCatchAll);
        }

        public override BoundNode VisitForEachStatement(BoundForEachStatement node)
        {
            var iterationVariableType = (BoundTypeExpression)this.Visit(node.IterationVariableType);
            LocalSymbol iterationVariableOpt = node.IterationVariableOpt == null ? null : (LocalSymbol)GetReplacementSymbol(node.IterationVariableOpt);
            var expression = (BoundExpression)this.Visit(node.Expression);
            BoundForEachDeconstructStep deconstructionOpt = node.DeconstructionOpt;
            var body = (BoundStatement)this.Visit(node.Body);
            return node.Update(
                node.EnumeratorInfoOpt,
                node.ElementConversion,
                iterationVariableType,
                iterationVariableOpt,
                expression,
                deconstructionOpt,
                body,
                node.Checked,
                node.BreakLabel,
                node.ContinueLabel);
        }

        public override BoundNode VisitForStatement(BoundForStatement node)
        {
            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            var initializer = (BoundStatement)Visit(node.Initializer);
            var condition = (BoundExpression)Visit(node.Condition);
            var increment = (BoundStatement)Visit(node.Increment);
            var body = (BoundStatement)Visit(node.Body);

            ImmutableArray<LocalSymbol> locals = _blockLocalsBuilder.ToImmutable();
            _blockLocalsBuilder = outerBlockLocalsBuilder;

            return node.Update(locals, initializer, condition, increment, body, node.BreakLabel, node.ContinueLabel);
        }

        public override BoundNode VisitLambda(BoundLambda node)
        {
            LambdaSymbol lambda = node.Symbol;

            var replacementLambda = new LambdaSymbol(
                _factory.CurrentMethod,
                lambda.Parameters,
                lambda.RefKind,
                lambda.ReturnType,
                lambda.MessageID,
                node.Syntax,
                lambda.IsSynthesizedLambda());

            // Creating a new lambda symbol automatically creates fresh parameter symbols so we need to populate the replacement symbols collection with them
            int parameterCount = lambda.ParameterCount;
            for (int i = 0; i < parameterCount; i++)
            {
                Debug.Assert(!_replacementSymbols.ContainsKey(lambda.Parameters[i]));
                _replacementSymbols = _replacementSymbols.Add(lambda.Parameters[i], replacementLambda.Parameters[i]);
            }

            MethodSymbol outerMethod = _factory.CurrentMethod;
            _factory.CurrentMethod = replacementLambda;

            var body = (BoundBlock)VisitWithExtraFlags(SplicedMethodBodyRewriterFlags.InNestedLambdaBody, node.Body);

            _factory.CurrentMethod = outerMethod;

            return node.Update(replacementLambda, body, node.Diagnostics, node.Binder, node.Type);
        }

        public override BoundNode VisitLocal(BoundLocal node)
        {
            var localSymbol = (LocalSymbol)GetReplacementSymbol(node.LocalSymbol);
            return node.Update(localSymbol, node.ConstantValue, node.Type);
        }

        public override BoundNode VisitLocalDeclaration(BoundLocalDeclaration node)
        {
            // All local variable declarations in the decorator method's body should be inside a block
            Debug.Assert(_blockLocalsBuilder != null);

            var localSymbol = (LocalSymbol)GetReplacementSymbol(node.LocalSymbol);
            _blockLocalsBuilder.Add(localSymbol);

            var initializerOpt = (BoundExpression)this.Visit(node.InitializerOpt);
            ImmutableArray<BoundExpression> argumentsOpt = VisitList(node.ArgumentsOpt);
            return node.Update(localSymbol, node.DeclaredType, initializerOpt, argumentsOpt);
        }

        public override BoundNode VisitParameter(BoundParameter node)
        {
            Symbol replacementSymbol = GetReplacementSymbol(node.ParameterSymbol);
            if (replacementSymbol is ParameterSymbol)
            {
                return node.Update((ParameterSymbol)replacementSymbol, node.Type);
            }
            else
            {
                Debug.Assert(replacementSymbol is LocalSymbol);
                return new BoundLocal(node.Syntax, (LocalSymbol)replacementSymbol, null, node.Type);
            }
        }

        public override BoundNode VisitReturnStatement(BoundReturnStatement node)
        {
            if (_flags.HasFlag(SplicedMethodBodyRewriterFlags.InNestedLambdaBody))
            {
                var expressionOpt = (BoundExpression)Visit(node.ExpressionOpt);
                return node.Update(node.RefKind, expressionOpt);
            }

            var gotoStatement = new BoundGotoStatement(node.Syntax, _postSpliceLabel);
            if (node.ExpressionOpt == null)
            {
                Debug.Assert(_tempResultLocal == null);

                return gotoStatement;
            }
            else
            {
                Debug.Assert(_tempResultLocal != null);

                var expressionOpt = (BoundExpression)Visit(node.ExpressionOpt);
                var resultAssignmentStatement = new BoundExpressionStatement(
                    node.Syntax,
                    new BoundAssignmentOperator(
                        node.Syntax,
                        new BoundLocal(node.Syntax, _tempResultLocal, null, _tempResultLocal.Type),
                        MetaUtils.ConvertIfNeeded(_tempResultLocal.Type, expressionOpt, _compilation),
                        RefKind.None,
                        _tempResultLocal.Type));
                return new BoundStatementList(
                    node.Syntax,
                    ImmutableArray.Create<BoundStatement>(resultAssignmentStatement, gotoStatement));
            }
        }

        public override BoundNode VisitStatementList(BoundStatementList node)
        {
            ImmutableArray<BoundStatement> statements = VisitList(node.Statements);
            return node.Update(FlattenStatementList(statements));
        }

        public override BoundNode VisitSwitchStatement(BoundSwitchStatement node)
        {
            Debug.Assert(node.LoweredPreambleOpt == null, "SplicedMethodBodyRewriter is not compatible with switch statements with a lowered preamble.");
            Debug.Assert(node.InnerLocalFunctions.IsEmpty, "SplicedMethodBodyRewriter is not compatible with switch statements with local functions.");

            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            var expression = (BoundExpression)Visit(node.Expression);
            ImmutableArray<BoundSwitchSection> switchSections = VisitList(node.SwitchSections);

            ImmutableArray<LocalSymbol> locals = _blockLocalsBuilder.ToImmutable();
            _blockLocalsBuilder = outerBlockLocalsBuilder;

            return node.Update(node.LoweredPreambleOpt, expression, node.ConstantTargetOpt, locals, node.InnerLocalFunctions, switchSections, node.BreakLabel, node.StringEquality);
        }

        public override BoundNode VisitUsingStatement(BoundUsingStatement node)
        {
            ImmutableArray<LocalSymbol>.Builder outerBlockLocalsBuilder = _blockLocalsBuilder;
            _blockLocalsBuilder = ImmutableArray.CreateBuilder<LocalSymbol>();

            var declarationsOpt = (BoundMultipleLocalDeclarations)Visit(node.DeclarationsOpt);
            var expressionOpt = (BoundExpression)Visit(node.ExpressionOpt);
            var body = (BoundStatement)Visit(node.Body);

            ImmutableArray<LocalSymbol> locals = _blockLocalsBuilder.ToImmutable();
            _blockLocalsBuilder = outerBlockLocalsBuilder;

            return node.Update(locals, declarationsOpt, expressionOpt, node.IDisposableConversion, body);
        }

        public override BoundNode VisitYieldBreakStatement(BoundYieldBreakStatement node)
        {
            _diagnostics.Add(ErrorCode.ERR_DecoratedIteratorMethod, node.Syntax.Location);
            return base.VisitYieldBreakStatement(node);
        }

        public override BoundNode VisitYieldReturnStatement(BoundYieldReturnStatement node)
        {
            _diagnostics.Add(ErrorCode.ERR_DecoratedIteratorMethod, node.Syntax.Location);
            return base.VisitYieldReturnStatement(node);
        }

        private static ImmutableArray<BoundStatement> FlattenStatementList(ImmutableArray<BoundStatement> originalStatements)
        {
            ImmutableArray<BoundStatement>.Builder statementsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (BoundStatement statement in originalStatements)
            {
                AddStatements(statement, statementsBuilder);
            }
            return statementsBuilder.ToImmutable();
        }

        private static void AddStatements(BoundStatement statement, ImmutableArray<BoundStatement>.Builder statementsBuilder)
        {
            if (statement.Kind == BoundKind.StatementList)
            {
                foreach (BoundStatement childStatement in ((BoundStatementList)statement).Statements)
                {
                    AddStatements(childStatement, statementsBuilder);
                }
            }
            else
            {
                statementsBuilder.Add(statement);
            }
        }

        private BoundNode VisitWithExtraFlags(SplicedMethodBodyRewriterFlags extraFlags, BoundNode node)
        {
            SplicedMethodBodyRewriterFlags oldFlags = _flags;
            _flags |= extraFlags;

            BoundNode result = Visit(node);

            _flags = oldFlags;

            return result;
        }

        private Symbol GetReplacementSymbol(Symbol originalSymbol)
        {
            Symbol replacementSymbol;
            if (!_replacementSymbols.TryGetValue(originalSymbol, out replacementSymbol))
            {
                // Target method and lambda parameters must already have been added to the collection
                Debug.Assert(originalSymbol.Kind == SymbolKind.Local);
                var local = (LocalSymbol)originalSymbol;
                replacementSymbol = _factory.SynthesizedLocal(
                    local.Type,
                    syntax: local.GetDeclaratorSyntax(),
                    kind: SynthesizedLocalKind.DecoratorLocal,
                    name: _variableNamesGenerator.GenerateFreshName(local.Name),
                    declarationKind: local.DeclarationKind,
                    constantValue: local.GetConstantValue(null, null));
                Debug.Assert(replacementSymbol != null);
                _replacementSymbols = _replacementSymbols.Add(originalSymbol, replacementSymbol);
            }
            return replacementSymbol;
        }
    }
}
