using System;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    [Flags]
    internal enum DecoratorMethodTypeCheckerFlags
    {
        None,
        ProhibitSpliceLocation = 1 << 0,
        InNestedLambdaBody = 1 << 1,
    }
}
