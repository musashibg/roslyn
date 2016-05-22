using Microsoft.CodeAnalysis.CSharp.Symbols;
using Roslyn.Utilities;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed class InTraitHostTypeBinder : Binder
    {
        private readonly SourceMemberContainerTypeSymbol _traitHostType;

        public InTraitHostTypeBinder(SourceMemberContainerTypeSymbol traitHostType, Binder next)
            : base(next)
        {
            _traitHostType = traitHostType;
        }

        internal override void LookupSymbolsInSingleBinder(
            LookupResult result,
            string name,
            int arity,
            ConsList<Symbol> basesBeingResolved,
            LookupOptions options,
            Binder originalBinder,
            bool diagnose,
            ref HashSet<DiagnosticInfo> useSiteDiagnostics)
        {
            Debug.Assert(result.IsClear);

            // first lookup members of the namespace
            if ((options & LookupOptions.NamespaceAliasesOnly) == 0)
            {
                this.LookupMembersInternal(result, _traitHostType, name, arity, basesBeingResolved, options, originalBinder, diagnose, ref useSiteDiagnostics);

                if (!result.IsClear)
                {
                    return;
                }
            }

            base.LookupSymbolsInSingleBinder(result, name, arity, basesBeingResolved, options, originalBinder, diagnose, ref useSiteDiagnostics);
        }

        internal override bool IsAccessibleHelper(
            Symbol symbol,
            TypeSymbol accessThroughType,
            out bool failedThroughTypeCheck,
            ref HashSet<DiagnosticInfo> useSiteDiagnostics,
            ConsList<Symbol> basesBeingResolved)
        {
            return this.IsSymbolAccessibleConditional(symbol, _traitHostType, accessThroughType, out failedThroughTypeCheck, ref useSiteDiagnostics);
        }
    }
}
