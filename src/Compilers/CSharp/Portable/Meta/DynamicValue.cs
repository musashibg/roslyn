namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class DynamicValue : CompileTimeValue
    {
        public static readonly DynamicValue Singleton = new DynamicValue();

        private DynamicValue()
            : base(CompileTimeValueKind.Dynamic)
        {
        }
    }
}
