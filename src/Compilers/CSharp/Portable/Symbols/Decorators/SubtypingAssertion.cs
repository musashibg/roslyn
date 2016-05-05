namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal struct SubtypingAssertion
    {
        public readonly ExtendedTypeInfo Supertype;
        public readonly ExtendedTypeInfo Subtype;

        public SubtypingAssertion(ExtendedTypeInfo supertype, ExtendedTypeInfo subtype)
        {
            Supertype = supertype;
            Subtype = subtype;
        }
    }
}
