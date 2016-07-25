// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
