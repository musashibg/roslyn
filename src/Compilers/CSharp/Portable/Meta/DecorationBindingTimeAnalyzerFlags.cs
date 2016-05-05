namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal enum DecorationBindingTimeAnalyzerFlags
    {
        None,
        InDynamicallyReachableCode = 1 << 0,
        InDynamicallyControlledLoop = InDynamicallyReachableCode | (1 << 1),
        InNestedLambdaBody = InDynamicallyReachableCode | (1 << 2),
    }
}
