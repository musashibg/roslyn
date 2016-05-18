namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal enum InterruptionKind
    {
        None,
        Continue = 1 << 0,
        Break = 1 << 1,

        // Only used when fully executing code at compile time
        Return = 1 << 2,
        Throw = 1 << 3,
    }
}
