namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal enum DecoratorMethodTypeCheckerFlags
    {
        None,
        ProhibitSpliceLocation = 1 << 0,
        InNestedLambdaBody = 1 << 1,
    }
}
