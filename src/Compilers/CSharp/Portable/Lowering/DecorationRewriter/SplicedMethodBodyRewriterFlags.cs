using System;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    [Flags]
    internal enum SplicedMethodBodyRewriterFlags
    {
        None,
        InNestedLambdaBody = 1 << 0,
    }
}
