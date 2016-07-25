// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    /// <summary>
    /// Corresponding to System.Reflection.BindingFlags
    /// </summary>
    [Flags]
    internal enum BindingFlags
    {
        Default = 0,
        IgnoreCase = 1 << 0,
        DeclaredOnly = 1 << 1,
        Instance = 1 << 2,
        Static = 1 << 3,
        Public = 1 << 4,
        NonPublic = 1 << 5,
        FlattenHierarchy = 1 << 6,
        InvokeMethod = 1 << 8,
        CreateInstance = 1 << 9,
        GetField = 1 << 10,
        SetField = 1 << 11,
        GetProperty = 1 << 12,
        SetProperty = 1 << 13,
        PutDispProperty = 1 << 14,
        PutRefDispProperty = 1 << 15,
        ExactBinding = 1 << 16,
        SuppressChangeType = 1 << 17,
        OptionalParamBinding = 1 << 18,
        IgnoreReturn = 1 << 24,
    }
}
