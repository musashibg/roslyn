using System;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    [Flags]
    internal enum EncapsulatingStatementKind
    {
        StatementMask = 0x000000FF,

        Conditional = 0x00000001,
        Switch = 0x00000002,
        Loop = 0x00000003,
        TryBlock = 0x00000004,
        CatchBlock = 0x00000005,
        FinallyBlock = 0x00000006,

        DynamicallyControlled = 0x00000100,
    }

    internal static class EncapsulatingStatementKindExtensions
    {
        public static EncapsulatingStatementKind Statement(this EncapsulatingStatementKind kind)
        {
            return kind & EncapsulatingStatementKind.StatementMask;
        }

        public static bool IsDynamic(this EncapsulatingStatementKind kind)
        {
            return kind.HasFlag(EncapsulatingStatementKind.DynamicallyControlled);
        }
    }
}
