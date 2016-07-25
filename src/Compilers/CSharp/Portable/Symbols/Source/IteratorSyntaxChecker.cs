using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed class IteratorSyntaxChecker : CSharpSyntaxWalker
    {
        public static bool IsIterator(CSharpSyntaxNode bodySyntax)
        {
            var iteratorSyntaxChecker = new IteratorSyntaxChecker();
            try
            {
                iteratorSyntaxChecker.Visit(bodySyntax);
            }
            catch (YieldFoundException)
            {
                return true;
            }
            return false;
        }

        public IteratorSyntaxChecker()
            : base(SyntaxWalkerDepth.Node)
        {
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            // We do not want to descend into anonymous methods, as they might be iterators regardless of whether the top-level method is
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            // We do not want to descend into local function definitions, as they might be iterators regardless of whether the top-level method is
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            // We do not want to descend into lambdas, as they might be iterators regardless of whether the top-level method is
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            // We do not want to descend into lambdas, as they might be iterators regardless of whether the top-level method is
        }

        public override void VisitYieldStatement(YieldStatementSyntax node)
        {
            throw new YieldFoundException();
        }

        private class YieldFoundException : Exception
        {
        }
    }
}
