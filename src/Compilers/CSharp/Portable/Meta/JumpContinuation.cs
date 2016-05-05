using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class JumpContinuation : ExecutionContinuation
    {
        private readonly LabelSymbol _label;

        public LabelSymbol Label
        {
            get { return _label; }
        }

        public JumpContinuation(LabelSymbol label)
            : base(ExecutionContinuationKind.Jump)
        {
            Debug.Assert(label != null);
            _label = label;
        }

        public override bool Equals(object obj)
        {
            var other = obj as JumpContinuation;
            if (other == null)
            {
                return false;
            }

            return Label == other.Label;
        }

        public override int GetHashCode()
        {
            return Label.GetHashCode();
        }
    }
}
