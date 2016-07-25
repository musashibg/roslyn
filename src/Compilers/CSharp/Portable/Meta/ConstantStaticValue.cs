// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class ConstantStaticValue : CompileTimeValue
    {
        private readonly ConstantValue _value;

        public ConstantValue Value
        {
            get { return _value; }
        }

        public ConstantStaticValue(ConstantValue value)
            : base(CompileTimeValueKind.Simple)
        {
            _value = value;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ConstantStaticValue;
            if (other == null)
            {
                return false;
            }

            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
