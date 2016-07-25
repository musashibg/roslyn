// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal class BindingTimeAnalysisResult
    {
        private readonly BindingTime _bindingTime;
        private readonly Symbol _mainSymbol;
        private readonly ImmutableHashSet<Symbol> _complexValuedSymbols;

        public BindingTime BindingTime
        {
            get { return _bindingTime; }
        }

        public Symbol MainSymbol
        {
            get { return _mainSymbol; }
        }

        public ImmutableHashSet<Symbol> ComplexValuedSymbols
        {
            get { return _complexValuedSymbols; }
        }

        public BindingTimeAnalysisResult(BindingTime bindingTime)
        {
            _bindingTime = bindingTime;
            _complexValuedSymbols = ImmutableHashSet<Symbol>.Empty;
        }

        public BindingTimeAnalysisResult(BindingTime bindingTime, Symbol mainSymbol, ImmutableHashSet<Symbol> complexValuedSymbols)
        {
            Debug.Assert(complexValuedSymbols != null);
            _bindingTime = bindingTime;
            _mainSymbol = mainSymbol;
            _complexValuedSymbols = complexValuedSymbols;
        }
    }
}
