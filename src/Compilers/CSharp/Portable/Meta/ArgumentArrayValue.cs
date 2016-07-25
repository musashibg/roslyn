// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class ArgumentArrayValue : CompileTimeValue
    {
        private readonly ImmutableArray<Symbol> _argumentSymbols;

        public ImmutableArray<Symbol> ArgumentSymbols
        {
            get { return _argumentSymbols; }
        }

        public ArgumentArrayValue(ImmutableArray<Symbol> argumentSymbols)
            : base(CompileTimeValueKind.ArgumentArray)
        {
            _argumentSymbols = argumentSymbols;
        }
    }
}
