// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class ConstructorInfoValue : CompileTimeValue
    {
        private readonly MethodSymbol _constructor;

        public MethodSymbol Constructor
        {
            get { return _constructor; }
        }

        public ConstructorInfoValue(MethodSymbol constructor)
            : base(CompileTimeValueKind.Complex)
        {
            _constructor = constructor;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ConstructorInfoValue;
            if (other == null)
            {
                return false;
            }

            return Constructor == other.Constructor;
        }

        public override int GetHashCode()
        {
            return Constructor.GetHashCode();
        }
    }
}
