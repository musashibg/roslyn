// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal partial class LanguageParser
    {
        private void ParseMetaDecorations(SyntaxListBuilder list, bool allowDecorators = true)
        {
            while (this.IsPossibleMetaDecoration())
            {
                var section = this.ParseMetaDecoration();
                if (!allowDecorators)
                {
                    section = this.AddError(section, ErrorCode.ERR_DecoratorsNotAllowed);
                }

                list.Add(section);
            }
        }

        private bool IsPossibleMetaDecoration()
        {
            return this.CurrentToken.Kind == SyntaxKind.PercentToken;
        }

        private MetaDecorationSyntax ParseMetaDecoration()
        {
            if (this.IsIncrementalAndFactoryContextMatches && this.CurrentNodeKind == SyntaxKind.MetaDecoration)
            {
                return (MetaDecorationSyntax)this.EatNode();
            }

            var percentToken = this.EatToken(SyntaxKind.PercentToken);

            var name = this.ParseQualifiedName();

            var argList = this.ParseMetaDecorationArgumentList();
            return _syntaxFactory.MetaDecoration(percentToken, name, argList);
        }

        private MetaDecorationArgumentListSyntax ParseMetaDecorationArgumentList()
        {
            if (this.IsIncrementalAndFactoryContextMatches && this.CurrentNodeKind == SyntaxKind.MetaDecorationArgumentList)
            {
                return (MetaDecorationArgumentListSyntax)this.EatNode();
            }

            MetaDecorationArgumentListSyntax argList = null;
            if (this.CurrentToken.Kind == SyntaxKind.OpenParenToken)
            {
                var openParen = this.EatToken(SyntaxKind.OpenParenToken);
                var argNodes = _pool.AllocateSeparated<MetaDecorationArgumentSyntax>();
                try
                {
                    bool shouldHaveName = false;
                    tryAgain:
                    if (this.CurrentToken.Kind != SyntaxKind.CloseParenToken)
                    {
                        if (this.IsPossibleMetaDecorationArgument() || this.CurrentToken.Kind == SyntaxKind.CommaToken)
                        {
                            // first argument
                            argNodes.Add(this.ParseMetaDecorationArgument(ref shouldHaveName));

                            // comma + argument or end?
                            while (true)
                            {
                                if (this.CurrentToken.Kind == SyntaxKind.CloseParenToken)
                                {
                                    break;
                                }
                                else if (this.CurrentToken.Kind == SyntaxKind.CommaToken || this.IsPossibleMetaDecorationArgument())
                                {
                                    argNodes.AddSeparator(this.EatToken(SyntaxKind.CommaToken));
                                    argNodes.Add(this.ParseMetaDecorationArgument(ref shouldHaveName));
                                }
                                else if (this.SkipBadMetaDecorationArgumentTokens(ref openParen, argNodes, SyntaxKind.CommaToken) == PostSkipAction.Abort)
                                {
                                    break;
                                }
                            }
                        }
                        else if (this.SkipBadMetaDecorationArgumentTokens(ref openParen, argNodes, SyntaxKind.IdentifierToken) == PostSkipAction.Continue)
                        {
                            goto tryAgain;
                        }
                    }

                    var closeParen = this.EatToken(SyntaxKind.CloseParenToken);
                    argList = _syntaxFactory.MetaDecorationArgumentList(openParen, argNodes, closeParen);
                }
                finally
                {
                    _pool.Free(argNodes);
                }
            }

            return argList;
        }

        private PostSkipAction SkipBadMetaDecorationArgumentTokens(ref SyntaxToken openParen, SeparatedSyntaxListBuilder<MetaDecorationArgumentSyntax> list, SyntaxKind expected)
        {
            return this.SkipBadSeparatedListTokensWithExpectedKind(ref openParen, list,
                p => p.CurrentToken.Kind != SyntaxKind.CommaToken && !p.IsPossibleMetaDecorationArgument(),
                p => p.CurrentToken.Kind == SyntaxKind.CloseParenToken || p.IsTerminator(),
                expected);
        }

        private bool IsPossibleMetaDecorationArgument()
        {
            return this.IsPossibleExpression();
        }

        private MetaDecorationArgumentSyntax ParseMetaDecorationArgument(ref bool shouldHaveName)
        {
            // Need to parse both "real" named arguments and attribute-style named arguments.
            // We track attribute-style named arguments only with fShouldHaveName.

            NameEqualsSyntax nameEquals = null;
            NameColonSyntax nameColon = null;
            if (this.CurrentToken.Kind == SyntaxKind.IdentifierToken)
            {
                SyntaxKind nextTokenKind = this.PeekToken(1).Kind;
                switch (nextTokenKind)
                {
                    case SyntaxKind.EqualsToken:
                        {
                            var name = this.ParseIdentifierToken();
                            var equals = this.EatToken(SyntaxKind.EqualsToken);
                            nameEquals = _syntaxFactory.NameEquals(_syntaxFactory.IdentifierName(name), equals);
                            shouldHaveName = true;
                        }

                        break;
                    case SyntaxKind.ColonToken:
                        {
                            var name = this.ParseIdentifierName();
                            var colonToken = this.EatToken(SyntaxKind.ColonToken);
                            nameColon = _syntaxFactory.NameColon(name, colonToken);
                            nameColon = CheckFeatureAvailability(nameColon, MessageID.IDS_FeatureNamedArgument);
                        }

                        break;
                }
            }

            var expr = this.ParseExpressionCore();

            // Not named -- give an error if it's supposed to be
            if (shouldHaveName && nameEquals == null)
            {
                expr = this.AddError(expr, ErrorCode.ERR_NamedArgumentExpected);
            }

            return _syntaxFactory.MetaDecorationArgument(nameEquals, nameColon, expr);
        }
    }
}
