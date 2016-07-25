// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal class SubtypingAssertionComparer : IEqualityComparer<SubtypingAssertion>
    {
        public static readonly SubtypingAssertionComparer Singleton = new SubtypingAssertionComparer();

        private SubtypingAssertionComparer()
        {
        }

        public bool Equals(SubtypingAssertion x, SubtypingAssertion y)
        {
            ExtendedTypeInfo xSupertype = x.Supertype;
            ExtendedTypeInfo xSubtype = x.Subtype;
            ExtendedTypeInfo ySupertype = y.Supertype;
            ExtendedTypeInfo ySubtype = y.Subtype;
            return xSupertype.Kind == ySupertype.Kind
                   && xSupertype.OrdinaryType == ySupertype.OrdinaryType
                   && xSupertype.ParameterIndexLocal == ySupertype.ParameterIndexLocal
                   && xSubtype.Kind == ySubtype.Kind
                   && xSubtype.OrdinaryType == ySubtype.OrdinaryType
                   && xSubtype.ParameterIndexLocal == ySubtype.ParameterIndexLocal;
        }

        public int GetHashCode(SubtypingAssertion obj)
        {
            return (int)obj.Supertype.Kind * 673
                   + obj.Supertype.OrdinaryType.GetHashCode() * 877
                   + (obj.Supertype.ParameterIndexLocal?.GetHashCode() ?? 0) * 569
                   + (int)obj.Subtype.Kind * 1193
                   + obj.Subtype.OrdinaryType.GetHashCode() * 397
                   + (obj.Subtype.ParameterIndexLocal?.GetHashCode() ?? 0) * 67;
        }
    }
}
