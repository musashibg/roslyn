// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class AttributeValue : CompileTimeValue
    {
        private readonly CSharpAttributeData _attribute;

        public CSharpAttributeData Attribute
        {
            get { return _attribute; }
        }

        public AttributeValue(CSharpAttributeData attribute)
            : base(CompileTimeValueKind.Complex)
        {
            _attribute = attribute;
        }

        public override bool Equals(object obj)
        {
            var other = obj as AttributeValue;
            if (other == null)
            {
                return false;
            }

            return Attribute == other.Attribute;
        }

        public override int GetHashCode()
        {
            return Attribute.GetHashCode();
        }
    }
}
