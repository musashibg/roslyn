// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class EnumValue : CompileTimeValue
    {
        private readonly TypeSymbol _enumType;
        private readonly ConstantValue _underlyingValue;

        public TypeSymbol EnumType
        {
            get { return _enumType; }
        }

        public ConstantValue UnderlyingValue
        {
            get { return _underlyingValue; }
        }

        public EnumValue(TypeSymbol enumType, ConstantValue underlyingValue)
            : base(CompileTimeValueKind.Simple)
        {
            _enumType = enumType;
            _underlyingValue = underlyingValue;
        }

        public override bool Equals(object obj)
        {
            var other = obj as EnumValue;
            if (other == null)
            {
                return false;
            }

            return EnumType == other.EnumType && UnderlyingValue == other.UnderlyingValue;
        }

        public override int GetHashCode()
        {
            return EnumType.GetHashCode() * 1549 + UnderlyingValue.GetHashCode();
        }
    }
}
