using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class MethodInfoValue : CompileTimeValue
    {
        private readonly MethodSymbol _method;

        public MethodSymbol Method
        {
            get { return _method; }
        }

        public MethodInfoValue(MethodSymbol method)
            : base(CompileTimeValueKind.Complex)
        {
            _method = method;
        }

        public override bool Equals(object obj)
        {
            var other = obj as MethodInfoValue;
            if (other == null)
            {
                return false;
            }

            return Method == other.Method;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode();
        }
    }
}
