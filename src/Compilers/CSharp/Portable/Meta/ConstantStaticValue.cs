namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class ConstantStaticValue : CompileTimeValue
    {
        private readonly ConstantValue _value;

        public ConstantValue Value
        {
            get { return _value; }
        }

        public ConstantStaticValue(ConstantValue value)
            : base(CompileTimeValueKind.Simple)
        {
            _value = value;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ConstantStaticValue;
            if (other == null)
            {
                return false;
            }

            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
