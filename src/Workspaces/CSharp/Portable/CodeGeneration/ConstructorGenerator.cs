﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CodeGeneration.CodeGenerationHelpers;
using static Microsoft.CodeAnalysis.CSharp.CodeGeneration.CSharpCodeGenerationHelpers;

namespace Microsoft.CodeAnalysis.CSharp.CodeGeneration
{
    internal static class ConstructorGenerator
    {
        private static MemberDeclarationSyntax LastConstructorOrField(SyntaxList<MemberDeclarationSyntax> members)
        {
            return LastConstructor(members) ?? LastField(members);
        }

        internal static TypeDeclarationSyntax AddConstructorTo(
            TypeDeclarationSyntax destination,
            IMethodSymbol constructor,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var constructorDeclaration = GenerateConstructorDeclaration(constructor, GetDestination(destination), options);

            // Generate after the last constructor, or after the last field, or at the start of the
            // type.
            var members = Insert(destination.Members, constructorDeclaration, options,
                availableIndices, after: LastConstructorOrField, before: FirstMember);

            return AddMembersTo(destination, members);
        }

        internal static ConstructorDeclarationSyntax GenerateConstructorDeclaration(
            IMethodSymbol constructor, CodeGenerationDestination destination, CodeGenerationOptions options)
        {
            options = options ?? CodeGenerationOptions.Default;

            var reusableSyntax = GetReuseableSyntaxNodeForSymbol<ConstructorDeclarationSyntax>(constructor, options);
            if (reusableSyntax != null)
            {
                return reusableSyntax;
            }

            bool hasNoBody = !options.GenerateMethodBodies;

            var declaration = SyntaxFactory.ConstructorDeclaration(
                attributeLists: AttributeGenerator.GenerateAttributeLists(constructor.GetAttributes(), options),
                decorators: default(SyntaxList<MetaDecorationSyntax>),
                modifiers: GenerateModifiers(constructor, options),
                identifier: CodeGenerationConstructorInfo.GetTypeName(constructor).ToIdentifierToken(),
                parameterList: ParameterGenerator.GenerateParameterList(constructor.Parameters, isExplicit: false, options: options),
                initializer: GenerateConstructorInitializer(constructor),
                body: hasNoBody ? null : GenerateBlock(constructor),
                semicolonToken: hasNoBody ? SyntaxFactory.Token(SyntaxKind.SemicolonToken) : default(SyntaxToken));

            return AddCleanupAnnotationsTo(
                ConditionallyAddDocumentationCommentTo(declaration, constructor, options));
        }

        private static ConstructorInitializerSyntax GenerateConstructorInitializer(
            IMethodSymbol constructor)
        {
            var arguments = CodeGenerationConstructorInfo.GetThisConstructorArgumentsOpt(constructor) ?? CodeGenerationConstructorInfo.GetBaseConstructorArgumentsOpt(constructor);
            var kind = CodeGenerationConstructorInfo.GetThisConstructorArgumentsOpt(constructor) != null
                ? SyntaxKind.ThisConstructorInitializer
                : SyntaxKind.BaseConstructorInitializer;

            return arguments == null
                ? null
                : SyntaxFactory.ConstructorInitializer(kind).WithArgumentList(GenerateArgumentList(arguments));
        }

        private static ArgumentListSyntax GenerateArgumentList(IList<SyntaxNode> arguments)
        {
            return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments.Select(ArgumentGenerator.GenerateArgument)));
        }

        private static BlockSyntax GenerateBlock(
            IMethodSymbol constructor)
        {
            var statements = CodeGenerationConstructorInfo.GetStatements(constructor) == null
                ? default(SyntaxList<StatementSyntax>)
                : StatementGenerator.GenerateStatements(CodeGenerationConstructorInfo.GetStatements(constructor));

            return SyntaxFactory.Block(statements);
        }

        private static SyntaxTokenList GenerateModifiers(IMethodSymbol constructor, CodeGenerationOptions options)
        {
            var tokens = new List<SyntaxToken>();

            if (constructor.IsStatic)
            {
                tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
            }
            else
            {
                AddAccessibilityModifiers(constructor.DeclaredAccessibility, tokens, options, Accessibility.Private);
            }

            return tokens.ToSyntaxTokenList();
        }
    }
}
