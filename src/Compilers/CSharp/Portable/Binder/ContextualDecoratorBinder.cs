namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed class ContextualDecoratorBinder : Binder
    {
        private readonly Symbol _decoratedMember;

        /// <param name="enclosing">Next binder in the chain (enclosing).</param>
        /// <param name="symbol">Symbol to which the attribute was applied (e.g. a parameter).</param>
        public ContextualDecoratorBinder(Binder enclosing, Symbol symbol)
            : base(enclosing)
        {
            _decoratedMember = GetDecoratedMember(symbol);
        }

        /// <summary>
        /// We're binding an decorator and this is the member to/in which the decorator was applied.
        /// </summary>
        /// <remarks>
        /// Method, property, event, or null.
        /// A virtual property on Binder (i.e. our usual pattern) would be more robust, but the applicability
        /// of this property is so narrow that it doesn't seem worthwhile.
        /// </remarks>
        internal Symbol DecoratedMember
        {
            get
            {
                return _decoratedMember;
            }
        }

        /// <summary>
        /// Walk up to the nearest method/property/event.
        /// </summary>
        private static Symbol GetDecoratedMember(Symbol symbol)
        {
            for (; (object)symbol != null; symbol = symbol.ContainingSymbol)
            {
                switch (symbol.Kind)
                {
                    case SymbolKind.Method:
                    case SymbolKind.Property:
                    case SymbolKind.Event:
                        return symbol;
                }
            }

            return symbol;
        }
    }
}
