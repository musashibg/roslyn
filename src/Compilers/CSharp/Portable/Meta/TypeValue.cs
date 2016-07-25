// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class TypeValue : CompileTimeValue
    {
        private readonly TypeSymbol _type;
        private readonly bool _isByRef;

        public TypeSymbol Type
        {
            get { return _type; }
        }

        public bool IsByRef
        {
            get { return _isByRef; }
        }

        public TypeValue(TypeSymbol type, bool isByRef = false)
            : base(CompileTimeValueKind.Simple)
        {
            Debug.Assert(type != null);
            _type = type;
            _isByRef = isByRef;
        }

        public override bool Equals(object obj)
        {
            var other = obj as TypeValue;
            if (other == null)
            {
                return false;
            }

            return Type == other.Type && IsByRef == other.IsByRef;
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode() * 1549 + IsByRef.GetHashCode();
        }
    }
}
