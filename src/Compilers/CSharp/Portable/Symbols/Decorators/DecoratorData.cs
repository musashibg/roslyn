﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal class DecoratorData
    {
        public SyntaxReference ApplicationSyntaxReference { get; }

        public NamedTypeSymbol DecoratorClass { get; }

        public MethodSymbol DecoratorConstructor { get; }

        public ImmutableArray<BoundExpression> ConstructorArguments { get; }

        public ImmutableArray<int> ConstructorArgumentsSourceIndices { get; }

        public ImmutableArray<KeyValuePair<string, BoundExpression>> NamedArguments { get; }

        public bool HasErrors { get; }

        internal DecoratorData(
            SyntaxReference applicationNode,
            NamedTypeSymbol decoratorClass,
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
            DecoratorClass = decoratorClass;
            DecoratorConstructor = decoratorConstructor;
            ConstructorArguments = constructorArguments;
            ConstructorArgumentsSourceIndices = constructorArgumentsSourceIndices;
            NamedArguments = namedArguments;
            HasErrors = hasErrors;
        }

        internal DecoratorData(SyntaxReference applicationNode, NamedTypeSymbol decoratorClass, MethodSymbol decoratorConstructor, bool hasErrors)
            : this(
            applicationNode,
            decoratorClass,
            decoratorConstructor,
            constructorArguments: ImmutableArray<BoundExpression>.Empty,
            constructorArgumentsSourceIndices: default(ImmutableArray<int>),
            namedArguments: ImmutableArray<KeyValuePair<string, BoundExpression>>.Empty,
            hasErrors: hasErrors)
        {
        }
    }
}
