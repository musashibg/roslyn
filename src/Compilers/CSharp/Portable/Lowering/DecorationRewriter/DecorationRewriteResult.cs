using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class DecorationRewriteResult
    {
        private readonly BoundNode _node;
        private readonly ImmutableDictionary<Symbol, CompileTimeValue> _updatedVariableValues;
        private readonly bool _mustEmit;
        private readonly CompileTimeValue _value;
        private readonly ImmutableHashSet<ExecutionContinuation> _possibleContinuations;

        public BoundNode Node
        {
            get { return _node; }
        }

        public ImmutableDictionary<Symbol, CompileTimeValue> UpdatedVariableValues
        {
            get { return _updatedVariableValues; }
        }

        public bool MustEmit
        {
            get { return _mustEmit; }
        }

        public CompileTimeValue Value
        {
            get { return _value; }
        }

        public ImmutableHashSet<ExecutionContinuation> PossibleContinuations
        {
            get { return _possibleContinuations; }
        }

        public bool HasAmbiguousContinuation
        {
            get
            {
                return PossibleContinuations.Count > 1;
            }
        }

        public bool HasNextStatementContinuation
        {
            get
            {
                return PossibleContinuations.Contains(ExecutionContinuation.NextStatement);
            }
        }

        public bool HasBreakContinuation
        {
            get
            {
                return PossibleContinuations.Contains(ExecutionContinuation.Break);
            }
        }

        public bool HasContinueContinuation
        {
            get
            {
                return PossibleContinuations.Contains(ExecutionContinuation.Continue);
            }
        }

        public bool HasReturnContinuation
        {
            get
            {
                return PossibleContinuations.Contains(ExecutionContinuation.Return);
            }
        }

        public bool HasThrowContinuation
        {
            get
            {
                return PossibleContinuations.Contains(ExecutionContinuation.Throw);
            }
        }

        public bool HasJumpContinuation
        {
            get
            {
                return PossibleContinuations.Any(ec => ec.Kind == ExecutionContinuationKind.Jump);
            }
        }

        public DecorationRewriteResult(BoundNode node, ImmutableDictionary<Symbol, CompileTimeValue> updatedVariableValues, bool mustEmit, CompileTimeValue value)
        {
            Debug.Assert(value != null);
            _node = node;
            _updatedVariableValues = updatedVariableValues;
            _mustEmit = mustEmit;
            _value = value;
        }

        public DecorationRewriteResult(
            BoundNode node,
            ImmutableDictionary<Symbol, CompileTimeValue> updatedVariableValues,
            bool mustEmit,
            ExecutionContinuation possibleContinuation)
            : this(node, updatedVariableValues, mustEmit, ImmutableHashSet.Create(possibleContinuation))
        {
        }

        public DecorationRewriteResult(
            BoundNode node,
            ImmutableDictionary<Symbol, CompileTimeValue> updatedVariableValues,
            bool mustEmit,
            ImmutableHashSet<ExecutionContinuation> possibleContinuations)
        {
            Debug.Assert(!(node is BoundExpression) && possibleContinuations != null && !possibleContinuations.IsEmpty);
            _node = node;
            _updatedVariableValues = updatedVariableValues;
            _mustEmit = mustEmit;
            _possibleContinuations = possibleContinuations;
        }
    }
}
