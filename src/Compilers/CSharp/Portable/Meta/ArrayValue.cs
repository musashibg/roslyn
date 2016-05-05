using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class ArrayValue : CompileTimeValue
    {
        private readonly ArrayTypeSymbol _arrayType;
        private readonly ImmutableArray<CompileTimeValue> _array;

        public ArrayTypeSymbol ArrayType
        {
            get { return _arrayType; }
        }

        public ImmutableArray<CompileTimeValue> Array
        {
            get { return _array; }
        }

        public ArrayValue(ArrayTypeSymbol arrayType, ImmutableArray<CompileTimeValue> array)
            : base(CompileTimeValueKind.Complex)
        {
            Debug.Assert(arrayType != null && !array.IsDefault);
            _arrayType = arrayType;
            _array = array;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ArrayValue;
            if (other == null)
            {
                return false;
            }

            return ArrayType == other.ArrayType && Array == other.Array;
        }

        public override int GetHashCode()
        {
            return ArrayType.GetHashCode() * 1549 + Array.GetHashCode();
        }

        public ArrayValue SetItem(int index, CompileTimeValue value)
        {
            Debug.Assert(index >= 0 && index < Array.Length);

            if (Array[index] == value)
            {
                return this;
            }
            else
            {
                return new ArrayValue(ArrayType, Array.SetItem(index, value));
            }
        }
    }
}
