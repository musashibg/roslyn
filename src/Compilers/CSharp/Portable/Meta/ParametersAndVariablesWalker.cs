using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class ParametersAndVariablesWalker : BoundTreeWalkerWithStackGuard
    {
        private readonly ImmutableHashSet<Symbol>.Builder _symbolsBuilder;

        private ParametersAndVariablesWalker(IEnumerable<Symbol> initialSymols)
        {
            _symbolsBuilder = ImmutableHashSet.CreateBuilder<Symbol>();
            foreach (Symbol symbol in initialSymols)
            {
                _symbolsBuilder.Add(symbol);
            }
        }

        public static ImmutableHashSet<Symbol> GetParametersAndVariables(SourceMemberMethodSymbol method)
        {
            var walker = new ParametersAndVariablesWalker(method.Parameters);

            BoundBlock methodBody = method.EarlyBoundBody;
            if (methodBody != null)
            {
                walker.Visit(methodBody);
            }

            return walker._symbolsBuilder.ToImmutable();
        }

        public override BoundNode VisitCatchBlock(BoundCatchBlock node)
        {
            if (!node.Locals.IsEmpty)
            {
                foreach (LocalSymbol local in node.Locals)
                {
                    _symbolsBuilder.Add(local);
                }
            }
            return base.VisitCatchBlock(node);
        }

        public override BoundNode VisitForEachStatement(BoundForEachStatement node)
        {
            LocalSymbol iterationVariableOpt = node.IterationVariableOpt;
            if (iterationVariableOpt != null)
            {
                _symbolsBuilder.Add(iterationVariableOpt);
            }
            return base.VisitForEachStatement(node);
        }

        public override BoundNode VisitLambda(BoundLambda node)
        {
            foreach (ParameterSymbol parameter in node.Symbol.Parameters)
            {
                _symbolsBuilder.Add(parameter);
            }
            return base.VisitLambda(node);
        }

        public override BoundNode VisitLocalDeclaration(BoundLocalDeclaration node)
        {
            _symbolsBuilder.Add(node.LocalSymbol);
            return base.VisitLocalDeclaration(node);
        }

        public override BoundNode VisitRangeVariable(BoundRangeVariable node)
        {
            _symbolsBuilder.Add(node.RangeVariableSymbol);
            return base.VisitRangeVariable(node);
        }
    }
}
