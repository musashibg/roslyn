using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Meta;
using System.Collections.Immutable;
using System.Threading;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal static class MetaclassApplicationPass
    {
        /// <summary>
        /// The metaclass application pass.  This pass looks up any metaclasses applied to the type declaration and
        /// statically executes their application methods, altering the target type.
        /// </summary>
        /// <param name="type">the type to be analyzed</param>
        /// <param name="compilationState">the current compilation state</param>
        /// <param name="diagnostics">the receiver of the reported diagnostics</param>
        /// <param name="cancellationToken">the cancellation token for the compilation</param>
        public static void ApplyMetaclasses(
            SourceMemberContainerTypeSymbol type,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics,
            CancellationToken cancellationToken)
        {
#if DEBUG
            int initialDiagnosticCount = diagnostics.ToReadOnly().Length;
#endif
            CSharpCompilation compilation = type.DeclaringCompilation;

            ImmutableArray<MetaclassData> metaclasses = type.GetMetaclasses();
            if (metaclasses.IsEmpty)
            {
                // Nothing to apply
                return;
            }

            // Metaclasses are applied in reverse order
            for (int metaclassOrdinal = metaclasses.Length - 1; metaclassOrdinal >= 0; metaclassOrdinal--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MetaclassData metaclass = metaclasses[metaclassOrdinal];
                if (!(metaclass.MetaclassClass is SourceMemberContainerTypeSymbol))
                {
                    continue;
                }

                var metaclassClass = (SourceMemberContainerTypeSymbol)metaclass.MetaclassClass;
                metaclassClass.WaitForCompletion(cancellationToken);
                if (metaclass.HasErrors || metaclassClass.HasDecoratorOrMetaclassMembersErrors)
                {
                    // Do not apply metaclasses which contain errors
                    continue;
                }

                MetaclassApplier.ApplyMetaclass(compilation, type, metaclass, compilationState, diagnostics, cancellationToken);
            }
        }
    }
}
