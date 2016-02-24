namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal partial class LanguageParser
    {
        private void ParseDecorators(SyntaxListBuilder list, bool allowDecorators = true)
        {
            while (this.IsPossibleDecorator())
            {
                var section = this.ParseDecorator();
                if (!allowDecorators)
                {
                    section = this.AddError(section, ErrorCode.ERR_DecoratorsNotAllowed);
                }

                list.Add(section);
            }
        }

        private bool IsPossibleDecorator()
        {
            return this.CurrentToken.Kind == SyntaxKind.PercentToken;
        }

        private DecoratorSyntax ParseDecorator()
        {
            if (this.IsIncrementalAndFactoryContextMatches && this.CurrentNodeKind == SyntaxKind.Decorator)
            {
                return (DecoratorSyntax)this.EatNode();
            }

            var percentToken = this.EatToken(SyntaxKind.PercentToken);

            var name = this.ParseQualifiedName();

            var argList = this.ParseDecoratorArgumentList();
            return _syntaxFactory.Decorator(percentToken, name, argList);
        }

        private DecoratorArgumentListSyntax ParseDecoratorArgumentList()
        {
            if (this.IsIncrementalAndFactoryContextMatches && this.CurrentNodeKind == SyntaxKind.DecoratorArgumentList)
            {
                return (DecoratorArgumentListSyntax)this.EatNode();
            }

            DecoratorArgumentListSyntax argList = null;
            if (this.CurrentToken.Kind == SyntaxKind.OpenParenToken)
            {
                var openParen = this.EatToken(SyntaxKind.OpenParenToken);
                var argNodes = _pool.AllocateSeparated<DecoratorArgumentSyntax>();
                try
                {
                    bool shouldHaveName = false;
                tryAgain:
                    if (this.CurrentToken.Kind != SyntaxKind.CloseParenToken)
                    {
                        if (this.IsPossibleDecoratorArgument() || this.CurrentToken.Kind == SyntaxKind.CommaToken)
                        {
                            // first argument
                            argNodes.Add(this.ParseDecoratorArgument(ref shouldHaveName));

                            // comma + argument or end?
                            while (true)
                            {
                                if (this.CurrentToken.Kind == SyntaxKind.CloseParenToken)
                                {
                                    break;
                                }
                                else if (this.CurrentToken.Kind == SyntaxKind.CommaToken || this.IsPossibleDecoratorArgument())
                                {
                                    argNodes.AddSeparator(this.EatToken(SyntaxKind.CommaToken));
                                    argNodes.Add(this.ParseDecoratorArgument(ref shouldHaveName));
                                }
                                else if (this.SkipBadDecoratorArgumentTokens(ref openParen, argNodes, SyntaxKind.CommaToken) == PostSkipAction.Abort)
                                {
                                    break;
                                }
                            }
                        }
                        else if (this.SkipBadDecoratorArgumentTokens(ref openParen, argNodes, SyntaxKind.IdentifierToken) == PostSkipAction.Continue)
                        {
                            goto tryAgain;
                        }
                    }

                    var closeParen = this.EatToken(SyntaxKind.CloseParenToken);
                    argList = _syntaxFactory.DecoratorArgumentList(openParen, argNodes, closeParen);
                }
                finally
                {
                    _pool.Free(argNodes);
                }
            }

            return argList;
        }

        private PostSkipAction SkipBadDecoratorArgumentTokens(ref SyntaxToken openParen, SeparatedSyntaxListBuilder<DecoratorArgumentSyntax> list, SyntaxKind expected)
        {
            return this.SkipBadSeparatedListTokensWithExpectedKind(ref openParen, list,
                p => p.CurrentToken.Kind != SyntaxKind.CommaToken && !p.IsPossibleDecoratorArgument(),
                p => p.CurrentToken.Kind == SyntaxKind.CloseParenToken || p.IsTerminator(),
                expected);
        }

        private bool IsPossibleDecoratorArgument()
        {
            return this.IsPossibleExpression();
        }

        private DecoratorArgumentSyntax ParseDecoratorArgument(ref bool shouldHaveName)
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

            return _syntaxFactory.DecoratorArgument(nameEquals, nameColon, expr);
        }
    }
}
