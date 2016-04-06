namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal enum DecoratorMethodTypeCheckerFlags
    {
        None,
        ProhibitSpliceLocation = 1 << 0,
        InNestedLambdaBody = 1 << 1,
    }
}
