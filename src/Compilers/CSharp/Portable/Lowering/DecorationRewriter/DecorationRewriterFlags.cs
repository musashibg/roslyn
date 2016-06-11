using System;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    [Flags]
    internal enum DecorationRewriterFlags
    {
        None,
        ProhibitSpliceLocation = 1 << 0,
        ExpectedDynamicArgumentArray = 1 << 1,
        InNestedLambdaBody = 1 << 2,
        InDecoratorArgument = 1 << 3,
    }
}
