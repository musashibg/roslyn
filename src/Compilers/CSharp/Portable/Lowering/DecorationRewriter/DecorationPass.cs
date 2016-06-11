using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using System.Collections.Immutable;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal static class DecorationPass
    {
        /// <summary>
        /// The decoration pass.  This pass looks up any decorators applied to the method declaration and
        /// rewrites the method body by splicing their code around it in a suitable fashion.
        /// </summary>
        /// <param name="method">the method to be analyzed</param>
        /// <param name="block">the method's body</param>
        /// <param name="compilationState">the current compilation state</param>
        /// <param name="diagnostics">the receiver of the reported diagnostics</param>
        /// <param name="cancellationToken">the cancellation token for the compilation</param>
        /// <returns>the rewritten block for the method</returns>
        public static BoundBlock Rewrite(
            MethodSymbol method,
            BoundBlock block,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics,
            CancellationToken cancellationToken)
        {
#if DEBUG
            int initialDiagnosticCount = diagnostics.ToReadOnly().Length;
#endif
            CSharpCompilation compilation = method.DeclaringCompilation;

            ImmutableArray<DecoratorData> decorators;
            if (method is SourcePropertyAccessorSymbol)
            {
                var associatedProperty = (PropertySymbol)method.AssociatedSymbol;
                decorators = associatedProperty.GetDecorators();
            }
            else
            {
                decorators = method.GetDecorators();
            }

            if (decorators.IsEmpty)
            {
                // Nothing to decorate
                return block;
            }

            // Decorators are applied in reverse order
            for (int decoratorOrdinal = decorators.Length - 1; decoratorOrdinal >= 0; decoratorOrdinal--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DecoratorData decorator = decorators[decoratorOrdinal];
                var decoratorClass = (SourceMemberContainerTypeSymbol)decorator.DecoratorClass;
                decoratorClass.ForceComplete(new SourceLocation(decorator.ApplicationSyntaxReference), cancellationToken);
                if (decorator.HasErrors || decoratorClass.HasDecoratorMethodErrors || decoratorClass.HasDecoratorOrMetaclassMembersErrors)
                {
                    // Do not apply decorators which contain errors
                    continue;
                }

                block = DecorationRewriter.Rewrite(compilation, method, block, decorator, decoratorOrdinal, compilationState, diagnostics, cancellationToken);
            }

            return block;
        }
    }
}
