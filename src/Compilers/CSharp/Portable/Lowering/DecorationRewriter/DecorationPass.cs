﻿using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
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
                cancellationToken.ThrowIfCancellationRequested();

                var decorator = decorators[decoratorOrdinal];
                if (decorator.HasErrors)
                {
                    // Do not apply decorators which contain errors
                    continue;
                }

                decorator.DecoratorClass.ForceComplete(new SourceLocation(decorator.ApplicationSyntaxReference), cancellationToken);

                block = DecorationRewriter.Rewrite(compilation, method, block, decorator, decoratorOrdinal, compilationState, diagnostics, cancellationToken);
            }

            return block;
        }
    }
}
