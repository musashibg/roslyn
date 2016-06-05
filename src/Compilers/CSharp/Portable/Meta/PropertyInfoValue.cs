﻿using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class PropertyInfoValue : CompileTimeValue
    {
        private readonly PropertySymbol _property;

        public PropertySymbol Property
        {
            get { return _property; }
        }

        public PropertyInfoValue(PropertySymbol property)
            : base(CompileTimeValueKind.Complex)
        {
            _property = property;
        }

        public override bool Equals(object obj)
        {
            var other = obj as PropertyInfoValue;
            if (other == null)
            {
                return false;
            }

            return Property == other.Property;
        }

        public override int GetHashCode()
        {
            return Property.GetHashCode();
        }
    }
}
