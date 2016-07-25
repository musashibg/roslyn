// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal struct SubtypingAssertion
    {
        public readonly ExtendedTypeInfo Supertype;
        public readonly ExtendedTypeInfo Subtype;

        public SubtypingAssertion(ExtendedTypeInfo supertype, ExtendedTypeInfo subtype)
        {
            Supertype = supertype;
            Subtype = subtype;
        }
    }
}
