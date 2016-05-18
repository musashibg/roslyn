using System;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class ExecutionInterruptionException : Exception
    {
        public InterruptionKind Interruption { get; }

        public ExecutionInterruptionException(InterruptionKind interruption)
        {
            Interruption = interruption;
        }
    }
}
