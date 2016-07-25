// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    [Flags]
    internal enum DecoratorMethodTypeCheckerFlags
    {
        None,
        ProhibitSpliceLocation = 1 << 0,
        InNestedLambdaBody = 1 << 1,
    }
}
