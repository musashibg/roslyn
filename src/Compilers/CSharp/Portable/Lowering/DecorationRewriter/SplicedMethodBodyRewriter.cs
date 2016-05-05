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
        private readonly DiagnosticBag _diagnostics;

        private ImmutableDictionary<Symbol, Symbol> _replacementSymbols;

        public SplicedMethodBodyRewriter(
            SyntheticBoundNodeFactory factory,
            LocalSymbol tempResultLocal,
            LabelSymbol postSpliceLabel,
            int spliceOrdinal,
            ImmutableDictionary<Symbol, Symbol> initialReplacementSymbols,
            DiagnosticBag diagnostics)
        {
            _compilation = factory.Compilation;
            _factory = factory;
            _tempResultLocal = tempResultLocal;
            _postSpliceLabel = postSpliceLabel;
            _spliceOrdinal = spliceOrdinal;
            _replacementSymbols = initialReplacementSymbols;
            _diagnostics = diagnostics;
        }

        // TODO: Handle multiple splices

        public static BoundBlock Rewrite(
            SyntheticBoundNodeFactory factory,
            LocalSymbol tempResultLocal,
            LabelSymbol postSpliceLabel,
            int spliceOrdinal,
            BoundBlock targetBody,
            ImmutableDictionary<Symbol, Symbol> initialReplacementSymbols,
            DiagnosticBag diagnostics)
        {
            var splicedMethodBodyRewriter = new SplicedMethodBodyRewriter(factory, tempResultLocal, postSpliceLabel, spliceOrdinal, initialReplacementSymbols, diagnostics);
            return (BoundBlock)splicedMethodBodyRewriter.VisitBlock(targetBody);
        }

        public override BoundNode VisitBlock(BoundBlock node)
        {
            ImmutableArray<BoundStatement> statements = VisitList(node.Statements);
            return node.Update(node.Locals, FlattenStatementList(statements));
        }

        //public override BoundNode VisitLocal(BoundLocal node)
        //{
        //    var localSymbol = (LocalSymbol)GetReplacementSymbol(node.LocalSymbol);
        //    return node.Update(localSymbol, node.ConstantValue, node.Type);
        //}

        //public override BoundNode VisitLocalDeclaration(BoundLocalDeclaration node)
        //{
        //    var localSymbol = (LocalSymbol)GetReplacementSymbol(node.LocalSymbol);
        //    var initializerOpt = (BoundExpression)this.Visit(node.InitializerOpt);
        //    ImmutableArray<BoundExpression> argumentsOpt = VisitList(node.ArgumentsOpt);
        //    return node.Update(localSymbol, node.DeclaredType, initializerOpt, argumentsOpt);
        //}

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

        private Symbol GetReplacementSymbol(Symbol originalSymbol)
        {
            Symbol replacementSymbol;
            if (!_replacementSymbols.TryGetValue(originalSymbol, out replacementSymbol))
            {
                replacementSymbol = originalSymbol;
                //if (originalSymbol.Kind == SymbolKind.Parameter)
                //{
                //    // This must be a nested lambda parameter, and we want to preserve it intact
                //    replacementSymbol = (ParameterSymbol)originalSymbol;
                //}
                //else
                //{
                //    Debug.Assert(originalSymbol.Kind == SymbolKind.Local);
                //    var local = (LocalSymbol)originalSymbol;
                //    replacementSymbol = _factory.SynthesizedLocal(local.Type, syntax: local.GetDeclaratorSyntax(), kind: SynthesizedLocalKind.DecoratorLocal);
                //}
                //Debug.Assert(replacementSymbol != null);
                //_replacementSymbols = _replacementSymbols.Add(originalSymbol, replacementSymbol);
            }
            return replacementSymbol;
        }
    }
}
