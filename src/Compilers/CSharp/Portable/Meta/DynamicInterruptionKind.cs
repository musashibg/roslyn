namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal enum DynamicInterruptionKind
    {
        None,
        Continue = 1 << 0,
        Break = 1 << 1,
    }
}
