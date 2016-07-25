// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class ExecutionContinuation
    {
        public static readonly ExecutionContinuation NextStatement = new ExecutionContinuation(ExecutionContinuationKind.NextStatement);
        public static readonly ExecutionContinuation Continue = new ExecutionContinuation(ExecutionContinuationKind.Continue);
        public static readonly ExecutionContinuation Break = new ExecutionContinuation(ExecutionContinuationKind.Break);
        public static readonly ExecutionContinuation Return = new ExecutionContinuation(ExecutionContinuationKind.Return);
        public static readonly ExecutionContinuation Throw = new ExecutionContinuation(ExecutionContinuationKind.Throw);

        private readonly ExecutionContinuationKind _kind;

        public ExecutionContinuationKind Kind
        {
            get { return _kind; }
        }

        protected ExecutionContinuation(ExecutionContinuationKind kind)
        {
            _kind = kind;
        }

        public bool AffectsLoopControlFlow()
        {
            return Kind == ExecutionContinuationKind.NextStatement || Kind == ExecutionContinuationKind.Break || Kind == ExecutionContinuationKind.Continue;
        }
    }
}
