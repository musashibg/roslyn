﻿// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal enum ExtendedTypeKind
    {
        OrdinaryType,
        ThisObject,
        ArgumentArray,
        Parameter,
        ReturnValue,
        MemberValue,
    }
}
