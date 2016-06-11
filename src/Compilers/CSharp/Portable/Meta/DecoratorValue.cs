using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class DecoratorValue : CompileTimeValue
    {
        private readonly NamedTypeSymbol _decoratorType;
        private readonly MethodSymbol _decoratorConstructor;
        private readonly ImmutableArray<BoundExpression> _constructorArguments;
        private readonly ImmutableArray<KeyValuePair<string, BoundExpression>> _namedArguments;

        public NamedTypeSymbol DecoratorType
        {
            get { return _decoratorType; }
        }

        public MethodSymbol DecoratorConstructor
        {
            get { return _decoratorConstructor; }
        }

        public ImmutableArray<BoundExpression> ConstructorArguments
        {
            get { return _constructorArguments; }
        }

        public ImmutableArray<KeyValuePair<string, BoundExpression>> NamedArguments
        {
            get { return _namedArguments; }
        }

        public DecoratorValue(
            NamedTypeSymbol decoratorType,
            MethodSymbol decoratorConstructor,
            ImmutableArray<BoundExpression> constructorArguments,
            ImmutableArray<KeyValuePair<string, BoundExpression>> namedArguments)
            : base(CompileTimeValueKind.Complex)
        {
            _decoratorType = decoratorType;
            _decoratorConstructor = decoratorConstructor;
            _constructorArguments = constructorArguments;
            _namedArguments = namedArguments;
        }

        public override bool Equals(object obj)
        {
            var other = obj as DecoratorValue;
            if (other == null)
            {
                return false;
            }

            return DecoratorType == other.DecoratorType
                   && DecoratorConstructor == other.DecoratorConstructor
                   && ConstructorArguments == other.ConstructorArguments
                   && NamedArguments == other.NamedArguments;
        }

        public override int GetHashCode()
        {
            return DecoratorType.GetHashCode() * 1549 + DecoratorConstructor.GetHashCode();
        }

        public DecoratorData CreateDecoratorData(SyntaxReference syntaxReference)
        {
            return new DecoratorData(syntaxReference, DecoratorType, DecoratorConstructor, ConstructorArguments, default(ImmutableArray<int>), NamedArguments, false);
        }
    }
}
