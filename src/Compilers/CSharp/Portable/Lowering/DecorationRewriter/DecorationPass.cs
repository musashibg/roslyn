using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal class DecorationPass
    {
        /// <summary>
        /// The decoration pass.  This pass looks up any decorators applied to the method declaration and
        /// rewrites the method body by splicing their code around it in a suitable fashion.
        /// </summary>
        /// <param name="method">the method to be analyzed</param>
        /// <param name="block">the method's body</param>
        /// <param name="compilationState">the current compilation state</param>
        /// <param name="diagnostics">the receiver of the reported diagnostics</param>
        /// <returns>the rewritten block for the method</returns>
        public static BoundBlock Rewrite(
            MethodSymbol method,
            BoundBlock block,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)
        {
#if DEBUG
            var initialDiagnosticCount = diagnostics.ToReadOnly().Length;
#endif
            var compilation = method.DeclaringCompilation;

            var decorators = method.GetDecorators();
            if (decorators.IsEmpty)
            {
                // Nothing to decorate
                return block;
            }

            // Decorators are applied in reverse order
            for (int decoratorOrdinal = decorators.Length - 1; decoratorOrdinal >= 0; decoratorOrdinal--)
            {
                var decorator = decorators[decoratorOrdinal];
                if (decorator.HasErrors)
                {
                    // Do not apply decorators which contain errors
                    continue;
                }

                block = DecorationRewriter.Rewrite(compilation, method, block, decorator, decoratorOrdinal, compilationState, diagnostics);
            }

            return block;
        }
    }
}
