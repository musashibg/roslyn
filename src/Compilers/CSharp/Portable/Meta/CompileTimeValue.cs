namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal abstract class CompileTimeValue
    {
        private readonly CompileTimeValueKind _kind;

        public static DynamicValue Dynamic
        {
            get { return DynamicValue.Singleton; }
        }

        public CompileTimeValueKind Kind
        {
            get { return _kind; }
        }

        protected CompileTimeValue(CompileTimeValueKind kind)
        {
            _kind = kind;
        }
    }
}
