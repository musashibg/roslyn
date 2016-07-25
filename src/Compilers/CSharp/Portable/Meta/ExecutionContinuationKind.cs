// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
