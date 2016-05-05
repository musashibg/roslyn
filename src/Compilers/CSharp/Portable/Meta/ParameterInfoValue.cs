using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class ParameterInfoValue : CompileTimeValue
    {
        private readonly ParameterSymbol _parameter;

        public ParameterSymbol Parameter
        {
            get { return _parameter; }
        }

        public ParameterInfoValue(ParameterSymbol parameter)
            : base(CompileTimeValueKind.Complex)
        {
            _parameter = parameter;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ParameterInfoValue;
            if (other == null)
            {
                return false;
            }

            return Parameter == other.Parameter;
        }

        public override int GetHashCode()
        {
            return Parameter.GetHashCode();
        }
    }
}
