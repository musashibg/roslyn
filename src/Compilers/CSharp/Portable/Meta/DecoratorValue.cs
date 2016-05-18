using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class DecoratorValue : CompileTimeValue
    {
        private readonly NamedTypeSymbol _decoratorType;
        private readonly MethodSymbol _decoratorConstructor;

        public NamedTypeSymbol DecoratorType
        {
            get { return _decoratorType; }
        }

        public MethodSymbol DecoratorConstructor
        {
            get { return _decoratorConstructor; }
        }

        public DecoratorValue(NamedTypeSymbol decoratorType, MethodSymbol decoratorConstructor)
            : base(CompileTimeValueKind.Complex)
        {
            _decoratorType = decoratorType;
            _decoratorConstructor = decoratorConstructor;
        }

        public override bool Equals(object obj)
        {
            var other = obj as DecoratorValue;
            if (other == null)
            {
                return false;
            }

            return DecoratorType == other.DecoratorType && DecoratorConstructor == other.DecoratorConstructor;
        }

        public override int GetHashCode()
        {
            return DecoratorType.GetHashCode() * 1549 + DecoratorConstructor.GetHashCode();
        }

        public DecoratorData CreateDecoratorData(SyntaxReference syntaxReference)
        {
            return new DecoratorData(syntaxReference, DecoratorType, DecoratorConstructor, false);
        }
    }
}
