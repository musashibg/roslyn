using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal class SplicedMethodBodyRewriter : BoundTreeRewriterWithStackGuard
    {
        private readonly SyntheticBoundNodeFactory _factory;
        private readonly LocalSymbol _tempResultLocal;
        private readonly LabelSymbol _postSpliceLabel;
        private readonly int _spliceOrdinal;
        private readonly DiagnosticBag _diagnostics;

        public SplicedMethodBodyRewriter(
            SyntheticBoundNodeFactory factory,
            LocalSymbol tempResultLocal,
            LabelSymbol postSpliceLabel,
            int spliceOrdinal,
            DiagnosticBag diagnostics)
        {
            _factory = factory;
            _tempResultLocal = tempResultLocal;
            _postSpliceLabel = postSpliceLabel;
            _spliceOrdinal = spliceOrdinal;
            _diagnostics = diagnostics;
        }

        // TODO: Handle multiple splices

        public static BoundBlock Rewrite(
            SyntheticBoundNodeFactory factory,
            LocalSymbol tempResultLocal,
            LabelSymbol postSpliceLabel,
            int spliceOrdinal,
            BoundBlock targetBody,
            DiagnosticBag diagnostics)
        {
            var splicedMethodBodyRewriter = new SplicedMethodBodyRewriter(factory, tempResultLocal, postSpliceLabel, spliceOrdinal, diagnostics);
            return (BoundBlock)splicedMethodBodyRewriter.VisitBlock(targetBody);
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
                        _factory.Convert(_tempResultLocal.Type, expressionOpt),
                        RefKind.None,
                        _tempResultLocal.Type));
                return new BoundStatementList(
                    node.Syntax,
                    ImmutableArray.Create<BoundStatement>(resultAssignmentStatement, gotoStatement));
            }
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
    }
}
