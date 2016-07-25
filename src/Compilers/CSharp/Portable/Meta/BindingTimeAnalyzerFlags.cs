// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    [Flags]
    internal enum BindingTimeAnalyzerFlags
    {
        None,
        InDynamicallyReachableCode = 1 << 0,
        InDynamicallyControlledLoop = InDynamicallyReachableCode | (1 << 1),
        InNestedLambdaBody = InDynamicallyReachableCode | (1 << 2),
        InMetaDecorationArgument = 1 << 3,
        InDecoratorCreationExpression = 1 << 4,
    }
}
