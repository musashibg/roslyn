// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal class MetaclassData
    {
        public SyntaxReference ApplicationSyntaxReference { get; }

        public NamedTypeSymbol MetaclassClass { get; }

        public MethodSymbol DecoratorConstructor { get; }

        public ImmutableArray<BoundExpression> ConstructorArguments { get; }

        public ImmutableArray<int> ConstructorArgumentsSourceIndices { get; }

        public ImmutableArray<KeyValuePair<string, BoundExpression>> NamedArguments { get; }

        public bool HasErrors { get; }

        internal MetaclassData(
            SyntaxReference applicationNode,
            NamedTypeSymbol metaclassClass,
            MethodSymbol decoratorConstructor,
            ImmutableArray<BoundExpression> constructorArguments,
            ImmutableArray<int> constructorArgumentsSourceIndices,
            ImmutableArray<KeyValuePair<string, BoundExpression>> namedArguments,
            bool hasErrors)
        {
            Debug.Assert(!constructorArguments.IsDefault);
            Debug.Assert(!namedArguments.IsDefault);
            Debug.Assert(constructorArgumentsSourceIndices.IsDefault ||
                constructorArgumentsSourceIndices.Any() && constructorArgumentsSourceIndices.Length == constructorArguments.Length);

            ApplicationSyntaxReference = applicationNode;
            MetaclassClass = metaclassClass;
            DecoratorConstructor = decoratorConstructor;
            ConstructorArguments = constructorArguments;
            ConstructorArgumentsSourceIndices = constructorArgumentsSourceIndices;
            NamedArguments = namedArguments;
            HasErrors = hasErrors;
        }

        internal MetaclassData(SyntaxReference applicationNode, NamedTypeSymbol metaclassClass, MethodSymbol decoratorConstructor, bool hasErrors)
            : this(
            applicationNode,
            metaclassClass,
            decoratorConstructor,
            constructorArguments: ImmutableArray<BoundExpression>.Empty,
            constructorArgumentsSourceIndices: default(ImmutableArray<int>),
            namedArguments: ImmutableArray<KeyValuePair<string, BoundExpression>>.Empty,
            hasErrors: hasErrors)
        {
        }
    }
}
