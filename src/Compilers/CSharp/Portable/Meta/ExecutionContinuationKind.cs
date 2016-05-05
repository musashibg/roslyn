namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal enum ExecutionContinuationKind
    {
        NextStatement,
        Continue,
        Break,
        Return,
        Throw,
        Jump,
    }
}
