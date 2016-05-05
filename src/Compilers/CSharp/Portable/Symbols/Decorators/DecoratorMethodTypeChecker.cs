using Microsoft.CodeAnalysis.CSharp.Meta;
using Roslyn.Utilities;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal class DecoratorMethodTypeChecker : BoundTreeVisitor<ImmutableHashSet<SubtypingAssertion>, DecoratorTypingResult>
    {
        private readonly CSharpCompilation _compilation;
        private readonly SourceMemberMethodSymbol _decoratorMethod;
        private readonly DiagnosticBag _diagnostics;

        private DecoratorMethodTypeCheckerFlags _flags;
        private ImmutableHashSet<LocalSymbol> _invalidatedLocals;
        private ImmutableHashSet<LocalSymbol> _blacklistedLocals;
        private ImmutableDictionary<Symbol, ExtendedTypeInfo> _variableTypes;
        private ImmutableHashSet<Symbol> _outerScopeVariables;

        public DecoratorMethodTypeChecker(
            CSharpCompilation compilation,
            SourceMemberMethodSymbol decoratorMethod,
            DiagnosticBag diagnostics)
        {
            _compilation = compilation;
            _decoratorMethod = decoratorMethod;
            _diagnostics = diagnostics;
            _invalidatedLocals = ImmutableHashSet.Create<LocalSymbol>();
            _blacklistedLocals = ImmutableHashSet.Create<LocalSymbol>();

            var variableTypesBuilder = ImmutableDictionary.CreateBuilder<Symbol, ExtendedTypeInfo>();
            variableTypesBuilder.Add(decoratorMethod.Parameters[0], new ExtendedTypeInfo(compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo)));
            variableTypesBuilder.Add(decoratorMethod.Parameters[1], ExtendedTypeInfo.CreateThisObjectType(compilation));
            variableTypesBuilder.Add(decoratorMethod.Parameters[2], ExtendedTypeInfo.CreateArgumentArrayType(compilation, false));
            _variableTypes = variableTypesBuilder.ToImmutable();

            _outerScopeVariables = ImmutableHashSet.Create<Symbol>();

            _flags = DecoratorMethodTypeCheckerFlags.None;
        }

        public static void PerformTypeCheck(
            CSharpCompilation compilation,
            SourceMemberMethodSymbol decoratorMethod,
            DiagnosticBag diagnostics)
        {
            if (!decoratorMethod.GetDecorators().IsEmpty)
            {
                diagnostics.Add(ErrorCode.ERR_DecoratedDecoratorMethod, decoratorMethod.SyntaxNode.Location);
                return;
            }

            var typeChecker = new DecoratorMethodTypeChecker(compilation, decoratorMethod, diagnostics);

            BoundBlock decoratorBody = decoratorMethod.EarlyBoundBody;
            if (decoratorBody == null)
            {
                diagnostics.Add(ErrorCode.ERR_DecoratorMethodWithoutBody, decoratorMethod.SyntaxNode.Location, decoratorMethod.ContainingType);
                return;
            }

            if (!diagnostics.HasAnyErrors() && !decoratorBody.HasAnyErrors)
            {
                typeChecker.Visit(decoratorBody, ImmutableHashSet.Create<SubtypingAssertion>(SubtypingAssertionComparer.Singleton));
            }
            decoratorMethod.DecoratorMethodVariableTypes = typeChecker._variableTypes;
        }

        public override DecoratorTypingResult Visit(BoundNode node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            Debug.Assert(node != null);

            if (node is BoundStatement)
            {
                Debug.Assert(_invalidatedLocals.IsEmpty);
                DecoratorTypingResult typingResult = base.Visit(node, subtypingAssertions);
                Debug.Assert(_invalidatedLocals.IsEmpty);
                return typingResult;
            }
            else
            {
                return base.Visit(node, subtypingAssertions);
            }
        }

        public override DecoratorTypingResult VisitAddressOfOperator(BoundAddressOfOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitAnonymousObjectCreationExpression(BoundAnonymousObjectCreationExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitArguments(node.Constructor, node.Arguments, default(ImmutableArray<RefKind>), ref subtypingAssertions);
            isSuccessful &= VisitList(node.Declarations, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitAnonymousPropertyDeclaration(BoundAnonymousPropertyDeclaration node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitArgList(BoundArgList node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitArgListOperator(BoundArgListOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitArrayAccess(BoundArrayAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult expressionTypingResult = Visit(node.Expression, subtypingAssertions);
            subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;

            ExtendedTypeInfo expressionType = expressionTypingResult.Type;
            if (expressionType.Kind == ExtendedTypeKind.ArgumentArray)
            {
                // If the array whose element is being accessed is a decorated method argument array, there must be a single index which is a local variable
                if (node.Indices.Length == 1 && node.Indices[0].Kind == BoundKind.Local)
                {
                    return new DecoratorTypingResult(
                        expressionTypingResult.IsSuccessful,
                        ExtendedTypeInfo.CreateParameterType(_compilation, ((BoundLocal)node.Indices[0]).LocalSymbol),
                        subtypingAssertions);
                }
                else
                {
                    expressionTypingResult = UpdateResultOnIncompatibleSpecialType(expressionTypingResult, ErrorCode.ERR_BadDecoratedMethodArgumentArrayIndex, node);
                }
            }
            bool isSuccessful = expressionTypingResult.IsSuccessful;
            isSuccessful &= VisitExpressions(node.Indices, ref subtypingAssertions);

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitArrayCreation(BoundArrayCreation node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitList(node.Bounds, ref subtypingAssertions);
            BoundArrayInitialization initializerOpt = node.InitializerOpt;
            if (initializerOpt != null)
            {
                DecoratorTypingResult initializerTypingResult = Visit(initializerOpt, subtypingAssertions);
                isSuccessful &= initializerTypingResult.IsSuccessful;
                subtypingAssertions = initializerTypingResult.UpdatedSubtypingAssertions;
            }
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitArrayInitialization(BoundArrayInitialization node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            for (int i = 0; i < node.Initializers.Length; i++)
            {
                BoundExpression argument = node.Initializers[i];
                DecoratorTypingResult argumentTypingResult = Visit(argument, subtypingAssertions);

                subtypingAssertions = argumentTypingResult.UpdatedSubtypingAssertions;

                ExtendedTypeInfo argumentType = argumentTypingResult.Type;
                if (!argumentType.IsOrdinaryType)
                {
                    switch (argumentType.Kind)
                    {
                        case ExtendedTypeKind.ThisObject:
                            if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(argument.Type), argumentType, subtypingAssertions))
                            {
                                argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodThisObjectCast, argument);
                            }
                            break;

                        case ExtendedTypeKind.ArgumentArray:
                            // Argument arrays cannot be passed to array initializers
                            argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, argument);
                            break;

                        case ExtendedTypeKind.ReturnValue:
                            if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(argument.Type), argumentType, subtypingAssertions))
                            {
                                argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodReturnValueCast, argument);
                            }
                            break;
                    }
                }
                isSuccessful &= argumentTypingResult.IsSuccessful;
            }
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitArrayLength(BoundArrayLength node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult expressionTypingResult = Visit(node.Expression, subtypingAssertions);
            return new DecoratorTypingResult(expressionTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), expressionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitAsOperator(BoundAsOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult operandTypingResult = Visit(node.Operand, subtypingAssertions);

            ValidateSpecialTypeConversion(ref operandTypingResult, operandTypingResult.UpdatedSubtypingAssertions, node);

            ExtendedTypeInfo operandType = operandTypingResult.Type;
            return new DecoratorTypingResult(
                operandTypingResult.IsSuccessful,
                new ExtendedTypeInfo(operandType.Kind, node.Type, operandType.IsAmbiguous, operandType.ParameterIndexLocal, operandType.RootSymbol),
                operandTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitAssignmentOperator(BoundAssignmentOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // If the left expression is a local variable, add it to the list of invalidated local variables
            BoundExpression left = node.Left;
            if (left.Kind == BoundKind.Local)
            {
                AddInvalidatedLocal(((BoundLocal)left).LocalSymbol, ref subtypingAssertions);
            }

            DecoratorTypingResult leftTypingResult = Visit(left, subtypingAssertions);
            BoundExpression right = node.Right;
            DecoratorTypingResult rightTypingResult = Visit(right, leftTypingResult.UpdatedSubtypingAssertions);

            if (left.Kind == BoundKind.Parameter
                && _decoratorMethod.Parameters.Contains(((BoundParameter)left).ParameterSymbol))
            {
                _diagnostics.Add(ErrorCode.ERR_DecoratorMethodParameterModification, left.Syntax.Location);
                leftTypingResult = leftTypingResult.WithIsSuccessful(false);
            }

            subtypingAssertions = rightTypingResult.UpdatedSubtypingAssertions;

            ValidateSpecialTypeAssignment(ref leftTypingResult, ref rightTypingResult, subtypingAssertions, left);

            // If the right expression is a decorated method argument array, check that the left side expression's type is also a decorated method argument array
            ExtendedTypeInfo rightType = rightTypingResult.Type;
            if (rightType.Kind == ExtendedTypeKind.ArgumentArray && !rightType.MatchesSpecialType(leftTypingResult.Type))
            {
                rightTypingResult = UpdateResultOnIncompatibleSpecialType(rightTypingResult, ErrorCode.ERR_DecoratedMethodArgumentArrayAssignedToOrdinaryArray, right);
            }

            return new DecoratorTypingResult(
                leftTypingResult.IsSuccessful && rightTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                subtypingAssertions);
        }

        public override DecoratorTypingResult VisitAttribute(BoundAttribute node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitAwaitExpression(BoundAwaitExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult expressionTypingResult = Visit(node.Expression, subtypingAssertions);
            return new DecoratorTypingResult(expressionTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), expressionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitBadExpression(BoundBadExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitList(node.ChildBoundNodes, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitBadStatement(BoundBadStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitList(node.ChildBoundNodes, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitBaseReference(BoundBaseReference node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // References to the decorator object would be meaningless in the target method's context
            _diagnostics.Add(ErrorCode.ERR_ThisReferenceInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitBinaryOperator(BoundBinaryOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult leftTypingResult = Visit(node.Left, subtypingAssertions);
            DecoratorTypingResult rightTypingResult = Visit(node.Right, leftTypingResult.UpdatedSubtypingAssertions);

            if (node.Type.SpecialType == SpecialType.System_Boolean)
            {
                // Boolean-valued binary operator expressions might be conditionals that introduce subtyping assertions, so we handle them separately
                ImmutableHashSet<SubtypingAssertion> assertionsIfTrue = null;
                ImmutableHashSet<SubtypingAssertion> assertionsIfFalse = null;
                switch (node.OperatorKind & BinaryOperatorKind.OpMask)
                {
                    case BinaryOperatorKind.Equal:
                        if (node.Left.Type == _compilation.GetWellKnownType(WellKnownType.System_Type))
                        {
                            ExtendedTypeInfo leftTypeValue = TryParseTypeFromExpression(node.Left);
                            ExtendedTypeInfo rightTypeValue = TryParseTypeFromExpression(node.Right);
                            if (leftTypeValue != null && rightTypeValue != null
                                && !(leftTypeValue.IsOrdinaryType && rightTypeValue.IsOrdinaryType))
                            {
                                assertionsIfTrue = ImmutableHashSet.Create(
                                    SubtypingAssertionComparer.Singleton,
                                    new SubtypingAssertion(leftTypeValue, rightTypeValue),
                                    new SubtypingAssertion(rightTypeValue, leftTypeValue)
                                );

                                // Check for comparison with void: a type being different from void means that it is safely castable to object
                                if (rightTypeValue.IsOrdinaryType && rightTypeValue.OrdinaryType.SpecialType == SpecialType.System_Void)
                                {
                                    assertionsIfFalse = ImmutableHashSet.Create(
                                        SubtypingAssertionComparer.Singleton,
                                        new SubtypingAssertion(new ExtendedTypeInfo(_compilation.ObjectType), leftTypeValue));
                                }
                                else if (leftTypeValue.IsOrdinaryType && leftTypeValue.OrdinaryType.SpecialType == SpecialType.System_Void)
                                {
                                    assertionsIfFalse = ImmutableHashSet.Create(
                                        SubtypingAssertionComparer.Singleton,
                                        new SubtypingAssertion(new ExtendedTypeInfo(_compilation.ObjectType), rightTypeValue));
                                }
                            }
                        }
                        break;

                    case BinaryOperatorKind.NotEqual:
                        if (node.Left.Type == _compilation.GetWellKnownType(WellKnownType.System_Type))
                        {
                            ExtendedTypeInfo leftTypeValue = TryParseTypeFromExpression(node.Left);
                            ExtendedTypeInfo rightTypeValue = TryParseTypeFromExpression(node.Right);
                            if (leftTypeValue != null && rightTypeValue != null
                                && !(leftTypeValue.IsOrdinaryType && rightTypeValue.IsOrdinaryType))
                            {
                                assertionsIfFalse = ImmutableHashSet.Create(
                                    SubtypingAssertionComparer.Singleton,
                                    new SubtypingAssertion(leftTypeValue, rightTypeValue),
                                    new SubtypingAssertion(rightTypeValue, leftTypeValue)
                                );

                                // Check for comparison with void: a type being different from void means that it is safely castable to object
                                if (rightTypeValue.IsOrdinaryType && rightTypeValue.OrdinaryType.SpecialType == SpecialType.System_Void)
                                {
                                    assertionsIfTrue = ImmutableHashSet.Create(
                                        SubtypingAssertionComparer.Singleton,
                                        new SubtypingAssertion(new ExtendedTypeInfo(_compilation.ObjectType), leftTypeValue));
                                }
                                else if (leftTypeValue.IsOrdinaryType && leftTypeValue.OrdinaryType.SpecialType == SpecialType.System_Void)
                                {
                                    assertionsIfTrue = ImmutableHashSet.Create(
                                        SubtypingAssertionComparer.Singleton,
                                        new SubtypingAssertion(new ExtendedTypeInfo(_compilation.ObjectType), rightTypeValue));
                                }
                            }
                        }
                        break;

                    case BinaryOperatorKind.And:
                        assertionsIfTrue = leftTypingResult.AssertionsIfTrue.Union(rightTypingResult.AssertionsIfTrue ?? EmptyAssertions);
                        assertionsIfFalse = leftTypingResult.AssertionsIfFalse.Intersect(rightTypingResult.AssertionsIfFalse ?? EmptyAssertions);
                        break;

                    case BinaryOperatorKind.Or:
                        assertionsIfTrue = leftTypingResult.AssertionsIfTrue.Intersect(rightTypingResult.AssertionsIfTrue ?? EmptyAssertions);
                        assertionsIfFalse = leftTypingResult.AssertionsIfFalse.Union(rightTypingResult.AssertionsIfFalse ?? EmptyAssertions);
                        break;
                }

                return new DecoratorTypingResult(
                    leftTypingResult.IsSuccessful && rightTypingResult.IsSuccessful,
                    new ExtendedTypeInfo(node.Type),
                    rightTypingResult.UpdatedSubtypingAssertions,
                    assertionsIfTrue,
                    assertionsIfFalse);
            }
            else
            {
                return new DecoratorTypingResult(
                    leftTypingResult.IsSuccessful && rightTypingResult.IsSuccessful,
                    new ExtendedTypeInfo(node.Type),
                    rightTypingResult.UpdatedSubtypingAssertions);
            }
        }

        public override DecoratorTypingResult VisitBlock(BoundBlock node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitList(node.Statements, ref subtypingAssertions);

            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitBreakStatement(BoundBreakStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitCall(BoundCall node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            MethodSymbol method = node.Method;

            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;

            #region Handle special method invocations

            if (CheckIsSpliceLocation(node))
            {
                if (_flags.HasFlag(DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation))
                {
                    _diagnostics.Add(ErrorCode.ERR_InvalidSpliceLocation, node.Syntax.Location);
                    return new DecoratorTypingResult(false, ExtendedTypeInfo.CreateReturnValueType(_compilation, false), subtypingAssertions);
                }

                // Ensure that the parameter or local variable passed as a last argument in the splice location invocation is a decorated method argument array
                BoundExpression argumentArrayArgument = node.Arguments[1];
                DecoratorTypingResult argumentArrayArgumentTypingResult = Visit(argumentArrayArgument, subtypingAssertions);
                if (argumentArrayArgumentTypingResult.Type.Kind != ExtendedTypeKind.ArgumentArray)
                {
                    argumentArrayArgumentTypingResult = UpdateResultOnIncompatibleSpecialType(
                        argumentArrayArgumentTypingResult,
                        ErrorCode.ERR_DecoratedMethodArgumentArrayVariableRequired,
                        argumentArrayArgument);
                }

                return new DecoratorTypingResult(
                    argumentArrayArgumentTypingResult.IsSuccessful,
                    ExtendedTypeInfo.CreateReturnValueType(_compilation, false),
                    argumentArrayArgumentTypingResult.UpdatedSubtypingAssertions);
            }

            if (CheckIsBaseDecoratorMethodCall(node))
            {
                // Base method calls are not allowed
                _diagnostics.Add(ErrorCode.ERR_BaseDecoratorMethodCallNotSupported, node.Syntax.Location);
                return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
            }

            if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__CloneArguments))
            {
                // MetaPrimitives.CloneArguments takes a decorated method argument array and returns another decorated method argument array
                Debug.Assert(node.Arguments.Length == 1);

                BoundExpression argument = node.Arguments[0];
                DecoratorTypingResult argumentTypingResult = Visit(argument, subtypingAssertions);
                if (argumentTypingResult.Type.Kind != ExtendedTypeKind.ArgumentArray)
                {
                    argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_DecoratedMethodArgumentArrayRequired, argument);
                }
                return new DecoratorTypingResult(
                    argumentTypingResult.IsSuccessful,
                    ExtendedTypeInfo.CreateArgumentArrayType(_compilation, false),
                    argumentTypingResult.UpdatedSubtypingAssertions);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__CloneArgumentsToObjectArray))
            {
                // MetaPrimitives.CloneArguments takes a decorated method argument array and returns a regular array of objects
                Debug.Assert(node.Arguments.Length == 1);

                BoundExpression argument = node.Arguments[0];
                DecoratorTypingResult argumentTypingResult = Visit(argument, subtypingAssertions);
                if (argumentTypingResult.Type.Kind != ExtendedTypeKind.ArgumentArray)
                {
                    argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_DecoratedMethodArgumentArrayRequired, argument);
                }
                return new DecoratorTypingResult(
                    argumentTypingResult.IsSuccessful,
                    new ExtendedTypeInfo(node.Type), // This should be object[]
                    argumentTypingResult.UpdatedSubtypingAssertions);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAssignableFrom))
            {
                // <type expression 1>.IsAssignableFrom(<type expression 2>) might introduce a subtyping assertion
                Debug.Assert(receiverOpt != null && node.Arguments.Length == 1 && node.Type.SpecialType == SpecialType.System_Boolean);

                ExtendedTypeInfo supertypeValue = TryParseTypeFromExpression(receiverOpt);
                ExtendedTypeInfo subtypeValue = TryParseTypeFromExpression(node.Arguments[0]);
                if (supertypeValue != null && subtypeValue != null
                    && !(supertypeValue.IsOrdinaryType && subtypeValue.IsOrdinaryType))
                {
                    ImmutableHashSet<SubtypingAssertion> assertionsIfTrue = ImmutableHashSet.Create(
                        SubtypingAssertionComparer.Singleton,
                        new SubtypingAssertion(supertypeValue, subtypeValue)
                    );

                    return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions, assertionsIfTrue);
                }
            }

            #endregion

            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);

                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                ExtendedTypeInfo receiverType = receiverTypingResult.Type;
                if (!receiverType.IsOrdinaryType)
                {
                    // Invoking arbitrary methods of an argument array is not allowed
                    if (receiverType.Kind == ExtendedTypeKind.ArgumentArray)
                    {
                        receiverTypingResult = UpdateResultOnIncompatibleSpecialType(receiverTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, receiverOpt);
                    }
                }
                isSuccessful &= receiverTypingResult.IsSuccessful;
            }

            isSuccessful &= VisitArguments(method, node.Arguments, node.ArgumentRefKindsOpt, ref subtypingAssertions);

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitCatchBlock(BoundCatchBlock node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            LocalSymbol localOpt = node.LocalOpt;
            if (localOpt != null)
            {
                // Add the exception variable to the typing context
                _variableTypes = _variableTypes.Add(localOpt, new ExtendedTypeInfo(localOpt.Type));
            }

            bool isSuccessful = true;
            ImmutableHashSet<SubtypingAssertion> innerSubtypingAssertions = subtypingAssertions;
            BoundExpression exceptionSourceOpt = node.ExceptionSourceOpt;
            if (exceptionSourceOpt != null)
            {
                DecoratorTypingResult exceptionSourceTypingResult = VisitAndCleanUpInvalidatedLocals(
                    node.ExceptionSourceOpt,
                    innerSubtypingAssertions,
                    DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation);
                isSuccessful &= exceptionSourceTypingResult.IsSuccessful;
                innerSubtypingAssertions = exceptionSourceTypingResult.UpdatedSubtypingAssertions;
            }

            ImmutableHashSet<SubtypingAssertion> assertionsIfFilterIsTrue = innerSubtypingAssertions;
            ImmutableHashSet<SubtypingAssertion> assertionsIfFilterIsFalse = innerSubtypingAssertions;
            BoundExpression exceptionFilterOpt = node.ExceptionFilterOpt;
            if (exceptionFilterOpt != null)
            {
                DecoratorTypingResult exceptionFilterTypingResult = VisitAndCleanUpInvalidatedLocals(
                    node.ExceptionFilterOpt,
                    innerSubtypingAssertions,
                    DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation);
                isSuccessful &= exceptionFilterTypingResult.IsSuccessful;
                assertionsIfFilterIsTrue = ExtendSubtypingAssertions(exceptionFilterTypingResult.UpdatedSubtypingAssertions, exceptionFilterTypingResult.AssertionsIfTrue);
                assertionsIfFilterIsFalse = ExtendSubtypingAssertions(exceptionFilterTypingResult.UpdatedSubtypingAssertions, exceptionFilterTypingResult.AssertionsIfFalse);
            }

            DecoratorTypingResult bodyTypingResult = Visit(node.Body, assertionsIfFilterIsTrue);
            isSuccessful &= bodyTypingResult.IsSuccessful;

            ImmutableHashSet<SubtypingAssertion> postCatchBlockAssertions = subtypingAssertions
                .Intersect(assertionsIfFilterIsFalse)
                .Intersect(bodyTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, null, postCatchBlockAssertions);
        }

        public override DecoratorTypingResult VisitCollectionElementInitializer(BoundCollectionElementInitializer node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitArguments(node.AddMethod, node.Arguments, default(ImmutableArray<RefKind>), ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitCollectionInitializerExpression(BoundCollectionInitializerExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitExpressions(node.Initializers, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitComplexConditionalReceiver(BoundComplexConditionalReceiver node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult valueTypeReceiverTypingResult = Visit(node.ValueTypeReceiver, subtypingAssertions);
            DecoratorTypingResult referenceTypeReceiverTypingResult = Visit(node.ReferenceTypeReceiver, valueTypeReceiverTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(
                valueTypeReceiverTypingResult.IsSuccessful && referenceTypeReceiverTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                referenceTypeReceiverTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // If the left expression is a local variable, add it to the list of invalidated local variables
            BoundExpression left = node.Left;
            if (left.Kind == BoundKind.Local)
            {
                AddInvalidatedLocal(((BoundLocal)left).LocalSymbol, ref subtypingAssertions);
            }

            DecoratorTypingResult leftTypingResult = Visit(left, subtypingAssertions);
            BoundExpression right = node.Right;
            DecoratorTypingResult rightTypingResult = Visit(right, leftTypingResult.UpdatedSubtypingAssertions);

            subtypingAssertions = rightTypingResult.UpdatedSubtypingAssertions;

            ValidateSpecialTypeAssignment(ref leftTypingResult, ref rightTypingResult, subtypingAssertions, left);

            return new DecoratorTypingResult(
                leftTypingResult.IsSuccessful && rightTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                subtypingAssertions);
        }

        public override DecoratorTypingResult VisitConditionalAccess(BoundConditionalAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult receiverTypingResult = Visit(node.Receiver, subtypingAssertions);
            DecoratorTypingResult accessExpressionTypingResult = Visit(node.AccessExpression, receiverTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(
                receiverTypingResult.IsSuccessful && accessExpressionTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                accessExpressionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitConditionalGoto(BoundConditionalGoto node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult conditionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Condition, subtypingAssertions);
            return new DecoratorTypingResult(conditionTypingResult.IsSuccessful, null, conditionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitConditionalOperator(BoundConditionalOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult conditionTypingResult = Visit(node.Condition, subtypingAssertions);
            bool isSuccessful = conditionTypingResult.IsSuccessful;

            BoundExpression consequence = node.Consequence;
            DecoratorTypingResult consequenceTypingResult = Visit(
                consequence,
                ExtendSubtypingAssertions(conditionTypingResult.UpdatedSubtypingAssertions, conditionTypingResult.AssertionsIfTrue));
            if (!consequenceTypingResult.Type.IsOrdinaryType)
            {
                // We do not allow special-typed values in the branches of a conditional operator, as reasoning about their type-safety would be overly complicated
                consequenceTypingResult = UpdateResultOnIncompatibleSpecialType(consequenceTypingResult, ErrorCode.ERR_DecoratorMethodSpecialTypeInConditionalOperator, consequence);
            }
            isSuccessful &= consequenceTypingResult.IsSuccessful;

            BoundExpression alternative = node.Alternative;
            DecoratorTypingResult alternativeTypingResult = Visit(
                node.Alternative,
                ExtendSubtypingAssertions(conditionTypingResult.UpdatedSubtypingAssertions, conditionTypingResult.AssertionsIfFalse));
            if (!alternativeTypingResult.Type.IsOrdinaryType)
            {
                // We do not allow special-typed values in the branches of a conditional operator, as reasoning about their type-safety would be overly complicated
                alternativeTypingResult = UpdateResultOnIncompatibleSpecialType(alternativeTypingResult, ErrorCode.ERR_DecoratorMethodSpecialTypeInConditionalOperator, alternative);
            }
            isSuccessful &= alternativeTypingResult.IsSuccessful;

            // Here we rely on the assumption that AssertionsIfTrue.Intersect(AssertionsIfFalse) should always be empty to ensure that the post-expression subtyping assertions are
            // a subset of the initial ones
            return new DecoratorTypingResult(
                isSuccessful,
                new ExtendedTypeInfo(node.Type),
                consequenceTypingResult.UpdatedSubtypingAssertions.Intersect(alternativeTypingResult.UpdatedSubtypingAssertions));
        }

        public override DecoratorTypingResult VisitConditionalReceiver(BoundConditionalReceiver node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitContinueStatement(BoundContinueStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitConversion(BoundConversion node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult operandTypingResult = Visit(node.Operand, subtypingAssertions);

            ValidateSpecialTypeConversion(ref operandTypingResult, operandTypingResult.UpdatedSubtypingAssertions, node);

            ExtendedTypeInfo operandType = operandTypingResult.Type;
            if (node.ConversionKind == ConversionKind.Boxing && !node.ExplicitCastInCode && node.Type.IsObjectType())
            {
                // Value types are boxed to Object before assignment to argument array elements and return value variables, which would interfere with subtyping assertion checking.
                // We therefore ignore implicit boxing operations and preserve the original operand's type
                return operandTypingResult;
            }
            else
            {
                return new DecoratorTypingResult(
                    operandTypingResult.IsSuccessful,
                    new ExtendedTypeInfo(operandType.Kind, node.Type, operandType.IsAmbiguous, operandType.ParameterIndexLocal, operandType.RootSymbol),
                    operandTypingResult.UpdatedSubtypingAssertions);
            }
        }

        public override DecoratorTypingResult VisitDecorator(BoundDecorator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitDefaultOperator(BoundDefaultOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitDelegateCreationExpression(BoundDelegateCreationExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult argumentTypingResult = Visit(node.Argument, subtypingAssertions);
            return new DecoratorTypingResult(argumentTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), argumentTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitDoStatement(BoundDoStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult bodyTypingResult, conditionTypingResult;
            bool areSubtypingAssertionsStable;
            bool isSuccessful = true;
            ImmutableHashSet<SubtypingAssertion> iterationSubtypingAssertions = subtypingAssertions;
            do
            {
                bodyTypingResult = Visit(node.Body, iterationSubtypingAssertions);
                isSuccessful &= bodyTypingResult.IsSuccessful;

                conditionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Condition, bodyTypingResult.UpdatedSubtypingAssertions, DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation);
                isSuccessful &= conditionTypingResult.IsSuccessful;

                ImmutableHashSet<SubtypingAssertion> nextIterationSubtypingAssertions = subtypingAssertions.Intersect(
                    ExtendSubtypingAssertions(conditionTypingResult.UpdatedSubtypingAssertions, conditionTypingResult.AssertionsIfTrue));
                areSubtypingAssertionsStable = iterationSubtypingAssertions.SetEquals(nextIterationSubtypingAssertions);
                iterationSubtypingAssertions = nextIterationSubtypingAssertions;
            }
            while (!areSubtypingAssertionsStable);

            // We do not add the assertions if condition is false to the final set of subtyping assertions, as we want to preserve the useful property that it is always a subset of the
            // initial subtyping assertions. This is necessary for proper propagation of guaranteed subtyping assertions to catch/finally blocks, where an exception might terminate the execution
            // of a statement in the corresponding try block prematurely
            return new DecoratorTypingResult(isSuccessful, null, conditionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitDup(BoundDup node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitDynamicCollectionElementInitializer(BoundDynamicCollectionElementInitializer node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitArguments(node.ApplicableMethods, node.Arguments, default(ImmutableArray<RefKind>), ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitDynamicIndexerAccess(BoundDynamicIndexerAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);
                isSuccessful &= receiverTypingResult.IsSuccessful;
                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;
            }
            isSuccessful &= VisitArguments(node.ApplicableIndexers, node.Arguments, node.ArgumentRefKindsOpt, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitDynamicInvocation(BoundDynamicInvocation node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult expressionTypingResult = Visit(node.Expression, subtypingAssertions);
            bool isSuccessful = expressionTypingResult.IsSuccessful;
            subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;

            isSuccessful &= VisitArguments(node.ApplicableMethods, node.Arguments, node.ArgumentRefKindsOpt, ref subtypingAssertions);

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitDynamicMemberAccess(BoundDynamicMemberAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult receiverTypingResult = Visit(node.Receiver, subtypingAssertions);
            return new DecoratorTypingResult(receiverTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), receiverTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitDynamicObjectCreationExpression(BoundDynamicObjectCreationExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitArguments(node.ApplicableMethods, node.Arguments, node.ArgumentRefKindsOpt, ref subtypingAssertions);

            BoundExpression initializerExpressionOpt = node.InitializerExpressionOpt;
            if (initializerExpressionOpt != null)
            {
                DecoratorTypingResult initializerExpressionTypingResult = Visit(initializerExpressionOpt, subtypingAssertions);
                isSuccessful &= initializerExpressionTypingResult.IsSuccessful;
                subtypingAssertions = initializerExpressionTypingResult.UpdatedSubtypingAssertions;
            }

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitDynamicObjectInitializerMember(BoundDynamicObjectInitializerMember node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitEventAccess(BoundEventAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);
                isSuccessful &= receiverTypingResult.IsSuccessful;
                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                // Argument arrays should not have events
                Debug.Assert(receiverTypingResult.Type.Kind != ExtendedTypeKind.ArgumentArray);
            }

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitEventAssignmentOperator(BoundEventAssignmentOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);
                isSuccessful &= receiverTypingResult.IsSuccessful;
                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                // Argument arrays should not have events
                Debug.Assert(receiverTypingResult.Type.Kind != ExtendedTypeKind.ArgumentArray);
            }

            DecoratorTypingResult argumentTypingResult = Visit(node.Argument, subtypingAssertions);

            return new DecoratorTypingResult(isSuccessful && argumentTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), argumentTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitExpressionStatement(BoundExpressionStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult expressionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Expression, subtypingAssertions);
            return new DecoratorTypingResult(expressionTypingResult.IsSuccessful, null, expressionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitFieldAccess(BoundFieldAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);

                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                ExtendedTypeInfo receiverType = receiverTypingResult.Type;
                if (!receiverType.IsOrdinaryType)
                {
                    // Accessing arbitrary fields of an argument array is not allowed
                    if (receiverType.Kind == ExtendedTypeKind.ArgumentArray)
                    {
                        receiverTypingResult = UpdateResultOnIncompatibleSpecialType(receiverTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, receiverOpt);
                    }
                }
                isSuccessful &= receiverTypingResult.IsSuccessful;
            }

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitFieldEqualsValue(BoundFieldEqualsValue node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult valueTypingResult = Visit(node.Value, subtypingAssertions);
            return new DecoratorTypingResult(valueTypingResult.IsSuccessful, null, valueTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitFieldInfo(BoundFieldInfo node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitFieldInitializer(BoundFieldInitializer node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitFixedLocalCollectionInitializer(BoundFixedLocalCollectionInitializer node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult expressionTypingResult = Visit(node.Expression, subtypingAssertions);
            return new DecoratorTypingResult(expressionTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), expressionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitFixedStatement(BoundFixedStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult declarationTypingResult = Visit(node.Declarations, subtypingAssertions);
            DecoratorTypingResult bodyTypingResult = Visit(node.Body, declarationTypingResult.UpdatedSubtypingAssertions);

            return new DecoratorTypingResult(
                declarationTypingResult.IsSuccessful && bodyTypingResult.IsSuccessful,
                null,
                bodyTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitForEachStatement(BoundForEachStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Add the foreach iteration variable to the typing context
            LocalSymbol iterationVariable = node.IterationVariable;
            _variableTypes = _variableTypes.Add(iterationVariable, new ExtendedTypeInfo(iterationVariable.Type));

            DecoratorTypingResult expressionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Expression, subtypingAssertions);
            bool isSuccessful = expressionTypingResult.IsSuccessful;

            DecoratorTypingResult bodyTypingResult;
            bool areSubtypingAssertionsStable;
            ImmutableHashSet<SubtypingAssertion> iterationSubtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;
            do
            {
                bodyTypingResult = Visit(node.Body, iterationSubtypingAssertions);
                isSuccessful &= bodyTypingResult.IsSuccessful;

                ImmutableHashSet<SubtypingAssertion> nextIterationSubtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions.Intersect(bodyTypingResult.UpdatedSubtypingAssertions);
                areSubtypingAssertionsStable = iterationSubtypingAssertions.SetEquals(nextIterationSubtypingAssertions);
                iterationSubtypingAssertions = nextIterationSubtypingAssertions;
            }
            while (!areSubtypingAssertionsStable);

            // The pre-iteration subtyping assertions are exactly the assertions that are guaranteed to hold after the loop is complete
            return new DecoratorTypingResult(isSuccessful, null, iterationSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitForStatement(BoundForStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult initializerTypingResult = VisitWithExtraFlags(DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation, node.Initializer, subtypingAssertions);
            bool isSuccessful = initializerTypingResult.IsSuccessful;

            DecoratorTypingResult conditionTypingResult, bodyTypingResult, incrementTypingResult;
            bool areSubtypingAssertionsStable;
            ImmutableHashSet<SubtypingAssertion> iterationSubtypingAssertions = initializerTypingResult.UpdatedSubtypingAssertions;
            do
            {
                conditionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Condition, iterationSubtypingAssertions, DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation);
                isSuccessful &= conditionTypingResult.IsSuccessful;

                bodyTypingResult = Visit(node.Body, ExtendSubtypingAssertions(conditionTypingResult.UpdatedSubtypingAssertions, conditionTypingResult.AssertionsIfTrue));
                isSuccessful &= bodyTypingResult.IsSuccessful;

                incrementTypingResult = VisitWithExtraFlags(DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation, node.Increment, bodyTypingResult.UpdatedSubtypingAssertions);
                isSuccessful &= incrementTypingResult.IsSuccessful;

                ImmutableHashSet<SubtypingAssertion> nextIterationSubtypingAssertions = subtypingAssertions.Intersect(incrementTypingResult.UpdatedSubtypingAssertions);
                areSubtypingAssertionsStable = iterationSubtypingAssertions.SetEquals(nextIterationSubtypingAssertions);
                iterationSubtypingAssertions = nextIterationSubtypingAssertions;
            }
            while (!areSubtypingAssertionsStable);

            // We do not add the assertions if condition is false to the final set of subtyping assertions, as we want to preserve the useful property that it is always a subset of the
            // initial subtyping assertions. This is necessary for proper propagation of guaranteed subtyping assertions to catch/finally blocks, where an exception might terminate the execution
            // of a statement in the corresponding try block prematurely
            return new DecoratorTypingResult(isSuccessful, null, conditionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitGlobalStatementInitializer(BoundGlobalStatementInitializer node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitGotoStatement(BoundGotoStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitHoistedFieldAccess(BoundHoistedFieldAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitHostObjectMemberReference(BoundHostObjectMemberReference node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should not exist in non-script code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitIfStatement(BoundIfStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult conditionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Condition, subtypingAssertions);
            bool isSuccessful = conditionTypingResult.IsSuccessful;

            DecoratorTypingResult consequenceTypingResult = Visit(
                node.Consequence,
                ExtendSubtypingAssertions(conditionTypingResult.UpdatedSubtypingAssertions, conditionTypingResult.AssertionsIfTrue));
            isSuccessful &= consequenceTypingResult.IsSuccessful;

            BoundStatement alternativeOpt = node.AlternativeOpt;
            if (alternativeOpt == null)
            {
                return new DecoratorTypingResult(
                    isSuccessful,
                    null,
                    conditionTypingResult.UpdatedSubtypingAssertions.Intersect(consequenceTypingResult.UpdatedSubtypingAssertions));
            }
            else
            {
                DecoratorTypingResult alternativeTypingResult = Visit(
                    alternativeOpt,
                    ExtendSubtypingAssertions(conditionTypingResult.UpdatedSubtypingAssertions, conditionTypingResult.AssertionsIfFalse));
                isSuccessful &= consequenceTypingResult.IsSuccessful;

                // Here we rely on the assumption that AssertionsIfTrue.Intersect(AssertionsIfFalse) should always be empty to ensure that the post-statement subtyping assertions are
                // a subset of the initial ones
                return new DecoratorTypingResult(
                    isSuccessful,
                    null,
                    consequenceTypingResult.UpdatedSubtypingAssertions.Intersect(alternativeTypingResult.UpdatedSubtypingAssertions));
            }
        }

        public override DecoratorTypingResult VisitImplicitReceiver(BoundImplicitReceiver node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitIncrementOperator(BoundIncrementOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            BoundExpression operand = node.Operand;
            DecoratorTypingResult operandTypingResult = Visit(operand, subtypingAssertions);

            // If the operand is a local variable, add it to the list of invalidated local variables
            if (operand.Kind == BoundKind.Local)
            {
                AddInvalidatedLocal(((BoundLocal)operand).LocalSymbol, ref subtypingAssertions);
            }

            return new DecoratorTypingResult(operandTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), operandTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitIndexerAccess(BoundIndexerAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);
                isSuccessful &= receiverTypingResult.IsSuccessful;
                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                // Argument arrays should not have indexers (array access is handled in VisitArrayAccess)
                Debug.Assert(receiverTypingResult.Type.Kind != ExtendedTypeKind.ArgumentArray);
            }
            isSuccessful &= VisitArguments(node.Indexer, node.Arguments, node.ArgumentRefKindsOpt, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitInterpolatedString(BoundInterpolatedString node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitExpressions(node.Parts, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitIsOperator(BoundIsOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult operandTypingResult = Visit(node.Operand, subtypingAssertions);
            return new DecoratorTypingResult(operandTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), operandTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitLabel(BoundLabel node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitLabelStatement(BoundLabelStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // A label in the code has the risk of introducing jumps from a different locations, and unifying their subtyping assertion sets is a task too complicated for this typing system.
            // Therefore, we discard all subtyping assertions and start with a blank slate
            return new DecoratorTypingResult(true, null, EmptyAssertions);
        }

        public override DecoratorTypingResult VisitLabeledStatement(BoundLabeledStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // A label in the code has the risk of introducing jumps from a different locations, and unifying their subtyping assertion sets is a task too complicated for this typing system.
            // Therefore, we discard all subtyping assertions and start with a blank slate
            DecoratorTypingResult bodyTypingResult = Visit(node.Body, EmptyAssertions);
            return new DecoratorTypingResult(bodyTypingResult.IsSuccessful, null, bodyTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitLambda(BoundLambda node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Store a set of outer scope variables, so that outer scope locals that might have been written to get blacklisted for the rest of the decorator method's body
            ImmutableHashSet<Symbol> oldOuterScopeVariables = _outerScopeVariables;
            _outerScopeVariables = _variableTypes.Keys.ToImmutableHashSet();

            // Add type information for all lambda parameters in the typing context
            LambdaSymbol lambda = node.Symbol;
            for (int i = 0; i < lambda.ParameterCount; i++)
            {
                ParameterSymbol parameter = lambda.Parameters[i];
                Debug.Assert(!_variableTypes.ContainsKey(parameter));
                _variableTypes = _variableTypes.Add(parameter, new ExtendedTypeInfo(parameter.Type));
            }

            // Visit the lambda's body
            ImmutableHashSet<LocalSymbol> oldInvalidatedLocals = _invalidatedLocals;
            _invalidatedLocals = ImmutableHashSet<LocalSymbol>.Empty;
            DecoratorTypingResult bodyTypingResult = VisitWithExtraFlags(DecoratorMethodTypeCheckerFlags.InNestedLambdaBody, node.Body, EmptyAssertions);
            _invalidatedLocals = oldInvalidatedLocals;

            // Restore the previous set of outer scope variables
            _outerScopeVariables = oldOuterScopeVariables;

            return new DecoratorTypingResult(
                bodyTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                InvalidateSubtypingAssertions(subtypingAssertions, _blacklistedLocals));
        }

        public override DecoratorTypingResult VisitLiteral(BoundLiteral node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitLocal(BoundLocal node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            ExtendedTypeInfo localType;
            if (_variableTypes.TryGetValue(node.LocalSymbol, out localType))
            {
                // The ordinary type of the local variable obtained from its declaration should match the bound local's type
                Debug.Assert(node.Type == localType.OrdinaryType);

                return new DecoratorTypingResult(true, localType, subtypingAssertions);
            }
            else
            {
                _diagnostics.Add(ErrorCode.ERR_NameNotInContext, node.Syntax.Location, node.LocalSymbol.Name);
                return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
            }
        }

        public override DecoratorTypingResult VisitLocalDeclaration(BoundLocalDeclaration node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            LocalSymbol local = node.LocalSymbol;
            Debug.Assert(!_variableTypes.ContainsKey(local));

            TypeSymbol localOrdinaryType = local.Type;
            ExtendedTypeInfo localExtendedType;
            if (localOrdinaryType.IsObjectType())
            {
                localExtendedType = ExtendedTypeInfo.CreateReturnValueType(_compilation, true, local);
            }
            else if (localOrdinaryType.IsArray() && ((ArrayTypeSymbol)localOrdinaryType).ElementType.IsObjectType())
            {
                localExtendedType = ExtendedTypeInfo.CreateArgumentArrayType(_compilation, true, local);
            }
            else
            {
                localExtendedType = new ExtendedTypeInfo(localOrdinaryType);
            }
            _variableTypes = _variableTypes.Add(local, localExtendedType);

            var localDeclarationTypingResult = new DecoratorTypingResult(true, localExtendedType, subtypingAssertions);

            BoundExpression initializerOpt = node.InitializerOpt;
            if (initializerOpt != null)
            {
                DecoratorTypingResult initializerTypingResult = VisitAndCleanUpInvalidatedLocals(initializerOpt, subtypingAssertions);

                // Set the declaration's type to the variable's type temporarily, so that we can validate the assignment if it is a special type
                localDeclarationTypingResult = localDeclarationTypingResult.Update(initializerTypingResult.IsSuccessful, localExtendedType, localDeclarationTypingResult.UpdatedSubtypingAssertions);

                ValidateSpecialTypeAssignment(ref localDeclarationTypingResult, ref initializerTypingResult, initializerTypingResult.UpdatedSubtypingAssertions, node);
                subtypingAssertions = localDeclarationTypingResult.UpdatedSubtypingAssertions;
            }

            bool argumentsSuccessful = VisitList(node.ArgumentsOpt, ref subtypingAssertions);
            localDeclarationTypingResult = localDeclarationTypingResult.Update(
                localDeclarationTypingResult.IsSuccessful && argumentsSuccessful,
                null,
                subtypingAssertions);

            return localDeclarationTypingResult;
        }

        public override DecoratorTypingResult VisitLockStatement(BoundLockStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult argumentTypingResult = VisitAndCleanUpInvalidatedLocals(node.Argument, subtypingAssertions);
            DecoratorTypingResult bodyTypingResult = Visit(node.Body, argumentTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(
                argumentTypingResult.IsSuccessful && bodyTypingResult.IsSuccessful,
                null,
                bodyTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitLoweredConditionalAccess(BoundLoweredConditionalAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult receiverTypingResult = Visit(node.Receiver, subtypingAssertions);
            bool isSuccessful = receiverTypingResult.IsSuccessful;

            BoundExpression whenNotNull = node.WhenNotNull;
            DecoratorTypingResult whenNotNullTypingResult = Visit(whenNotNull, receiverTypingResult.UpdatedSubtypingAssertions);
            if (!whenNotNullTypingResult.Type.IsOrdinaryType)
            {
                // We do not allow special-typed values in the branches of a conditional operator, as reasoning about their type-safety would be overly complicated
                whenNotNullTypingResult = UpdateResultOnIncompatibleSpecialType(whenNotNullTypingResult, ErrorCode.ERR_DecoratorMethodSpecialTypeInConditionalOperator, whenNotNull);
            }
            isSuccessful &= whenNotNullTypingResult.IsSuccessful;

            BoundExpression whenNullOpt = node.WhenNullOpt;
            if (whenNullOpt == null)
            {
                return new DecoratorTypingResult(
                    isSuccessful,
                    new ExtendedTypeInfo(node.Type),
                    receiverTypingResult.UpdatedSubtypingAssertions.Intersect(whenNotNullTypingResult.UpdatedSubtypingAssertions));
            }
            else
            {
                DecoratorTypingResult whenNullTypingResult = Visit(whenNullOpt, receiverTypingResult.UpdatedSubtypingAssertions);
                if (!whenNullTypingResult.Type.IsOrdinaryType)
                {
                    // We do not allow special-typed values in the branches of a conditional operator, as reasoning about their type-safety would be overly complicated
                    whenNullTypingResult = UpdateResultOnIncompatibleSpecialType(whenNullTypingResult, ErrorCode.ERR_DecoratorMethodSpecialTypeInConditionalOperator, whenNullOpt);
                }
                isSuccessful &= whenNullTypingResult.IsSuccessful;
                return new DecoratorTypingResult(
                    isSuccessful,
                    new ExtendedTypeInfo(node.Type),
                    whenNotNullTypingResult.UpdatedSubtypingAssertions.Intersect(whenNullTypingResult.UpdatedSubtypingAssertions));
            }
        }

        public override DecoratorTypingResult VisitMakeRefOperator(BoundMakeRefOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitMethodGroup(BoundMethodGroup node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);

                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                ExtendedTypeInfo receiverType = receiverTypingResult.Type;
                if (!receiverType.IsOrdinaryType)
                {
                    // Accessing arbitrary methods of an argument array is not allowed
                    if (receiverType.Kind == ExtendedTypeKind.ArgumentArray)
                    {
                        receiverTypingResult = UpdateResultOnIncompatibleSpecialType(receiverTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, receiverOpt);
                    }
                }
                isSuccessful &= receiverTypingResult.IsSuccessful;
            }

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitMethodInfo(BoundMethodInfo node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitMultipleLocalDeclarations(BoundMultipleLocalDeclarations node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitList(node.LocalDeclarations, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitNameOfOperator(BoundNameOfOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult argumentTypingResult = Visit(node.Argument, subtypingAssertions);
            Debug.Assert(subtypingAssertions == argumentTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(argumentTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), argumentTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitNamespaceExpression(BoundNamespaceExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should not exist inside a method's body
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitNewT(BoundNewT node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression initializerExpressionOpt = node.InitializerExpressionOpt;
            if (initializerExpressionOpt != null)
            {
                DecoratorTypingResult expressionTypingResult = Visit(initializerExpressionOpt, subtypingAssertions);
                isSuccessful &= expressionTypingResult.IsSuccessful;
                subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;
            }
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitNoOpStatement(BoundNoOpStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitNoPiaObjectCreationExpression(BoundNoPiaObjectCreationExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression initializerExpressionOpt = node.InitializerExpressionOpt;
            if (initializerExpressionOpt != null)
            {
                DecoratorTypingResult expressionTypingResult = Visit(initializerExpressionOpt, subtypingAssertions);
                isSuccessful &= expressionTypingResult.IsSuccessful;
                subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;
            }
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitNullCoalescingOperator(BoundNullCoalescingOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult leftOperandTypingResult = Visit(node.LeftOperand, subtypingAssertions);
            DecoratorTypingResult rightOperandTypingResult = Visit(node.RightOperand, leftOperandTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(
                leftOperandTypingResult.IsSuccessful && rightOperandTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                rightOperandTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitObjectCreationExpression(BoundObjectCreationExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitArguments(node.Constructor, node.Arguments, node.ArgumentRefKindsOpt, ref subtypingAssertions);

            BoundExpression initializerExpressionOpt = node.InitializerExpressionOpt;
            if (initializerExpressionOpt != null)
            {
                DecoratorTypingResult expressionTypingResult = Visit(initializerExpressionOpt, subtypingAssertions);
                isSuccessful &= expressionTypingResult.IsSuccessful;
                subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;
            }

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitObjectInitializerExpression(BoundObjectInitializerExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitExpressions(node.Initializers, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitObjectInitializerMember(BoundObjectInitializerMember node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitArguments(node.MemberSymbol, node.Arguments, node.ArgumentRefKindsOpt, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitParameter(BoundParameter node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            ExtendedTypeInfo parameterType;
            if (_variableTypes.TryGetValue(node.ParameterSymbol, out parameterType))
            {
                // The ordinary type of the parameter obtained from its declaration should match the bound parameter's type
                Debug.Assert(node.Type == parameterType.OrdinaryType);

                return new DecoratorTypingResult(true, parameterType, subtypingAssertions);
            }
            else
            {
                _diagnostics.Add(ErrorCode.ERR_NameNotInContext, node.Syntax.Location, node.ParameterSymbol.Name);
                return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
            }
        }

        public override DecoratorTypingResult VisitParameterEqualsValue(BoundParameterEqualsValue node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult valueTypingResult = Visit(node.Value, subtypingAssertions);
            return new DecoratorTypingResult(valueTypingResult.IsSuccessful, null, valueTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitPointerElementAccess(BoundPointerElementAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitPointerIndirectionOperator(BoundPointerIndirectionOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitPreviousSubmissionReference(BoundPreviousSubmissionReference node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should not exist in non-script code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitPropertyAccess(BoundPropertyAccess node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                // Handle special property accesses
                if (CheckIsSpecificParameter(receiverOpt, _decoratorMethod.Parameters[0])
                    && node.PropertySymbol == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__IsStatic))
                {
                    Debug.Assert(node.Type.SpecialType == SpecialType.System_Boolean);

                    // The decorated method not being static means that its this-reference is safely castable to object
                    ImmutableHashSet<SubtypingAssertion> assertionsIfFalse = ImmutableHashSet.Create(
                        SubtypingAssertionComparer.Singleton,
                        new SubtypingAssertion(new ExtendedTypeInfo(_compilation.ObjectType), ExtendedTypeInfo.CreateThisObjectType(_compilation)));
                    return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions, assertionsIfFalse: assertionsIfFalse);
                }

                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);

                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                ExtendedTypeInfo receiverType = receiverTypingResult.Type;
                if (!receiverType.IsOrdinaryType)
                {
                    // Accessing arbitrary properties of an argument array is not allowed (arguments.Length is handled by VisitArrayLength)
                    if (receiverType.Kind == ExtendedTypeKind.ArgumentArray)
                    {
                        receiverTypingResult = UpdateResultOnIncompatibleSpecialType(receiverTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, receiverOpt);
                    }
                }
                isSuccessful &= receiverTypingResult.IsSuccessful;
            }

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitPropertyEqualsValue(BoundPropertyEqualsValue node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult valueTypingResult = Visit(node.Value, subtypingAssertions);
            return new DecoratorTypingResult(valueTypingResult.IsSuccessful, null, valueTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitPropertyGroup(BoundPropertyGroup node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression receiverOpt = node.ReceiverOpt;
            if (receiverOpt != null && receiverOpt.Kind != BoundKind.TypeExpression)
            {
                DecoratorTypingResult receiverTypingResult = Visit(receiverOpt, subtypingAssertions);

                subtypingAssertions = receiverTypingResult.UpdatedSubtypingAssertions;

                ExtendedTypeInfo receiverType = receiverTypingResult.Type;
                if (!receiverType.IsOrdinaryType)
                {
                    // Accessing arbitrary properties of an argument array is not allowed (arguments.Length is handled by VisitArrayLength)
                    if (receiverType.Kind == ExtendedTypeKind.ArgumentArray)
                    {
                        receiverTypingResult = UpdateResultOnIncompatibleSpecialType(receiverTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, receiverOpt);
                    }
                }
                isSuccessful &= receiverTypingResult.IsSuccessful;
            }

            return new DecoratorTypingResult(isSuccessful, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitPseudoVariable(BoundPseudoVariable node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitQueryClause(BoundQueryClause node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult valueTypingResult = Visit(node.Value, subtypingAssertions);
            return new DecoratorTypingResult(valueTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), valueTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitRangeVariable(BoundRangeVariable node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult valueTypingResult = Visit(node.Value, subtypingAssertions);
            return new DecoratorTypingResult(valueTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), valueTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitRefTypeOperator(BoundRefTypeOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitRefValueOperator(BoundRefValueOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_LanguageFeatureNotSupportedInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitReturnStatement(BoundReturnStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            ExtendedTypeInfo returnValueType = ExtendedTypeInfo.CreateReturnValueType(_compilation, false);

            BoundExpression expressionOpt = node.ExpressionOpt;
            if (expressionOpt == null)
            {
                // The decorator method itself should always have a return operand expression
                Debug.Assert(_flags.HasFlag(DecoratorMethodTypeCheckerFlags.InNestedLambdaBody));
                return new DecoratorTypingResult(true, null, subtypingAssertions);
            }

            DecoratorTypingResult expressionTypingResult = VisitAndCleanUpInvalidatedLocals(node.ExpressionOpt, subtypingAssertions);
            subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;

            if (!_flags.HasFlag(DecoratorMethodTypeCheckerFlags.InNestedLambdaBody))
            {
                // If we are not inside a nested lambda, we should validate the return expression's type
                ExtendedTypeInfo expressionType = expressionTypingResult.Type;
                if (expressionType.IsAmbiguous)
                {
                    // If the right expression has an ambiguous type, it is enough for either one of the alternatives to be assignable to the decorated method's return type.
                    // We update it to an inambiguous type if one of the alternatives is rejected
                    ExtendedTypeInfo rightSpecialType = expressionType.UpdateToUnambiguousSpecialType();
                    bool isRightSpecialTypeCompatible = CheckSpecialTypeIsAssignableFrom(returnValueType, rightSpecialType, subtypingAssertions);
                    ExtendedTypeInfo rightOrdinaryType = expressionType.UpdateToUnambiguousOrdinaryType();
                    bool isRightOrdinaryTypeCompatible = CheckSpecialTypeIsAssignableFrom(returnValueType, rightOrdinaryType, subtypingAssertions);
                    if (isRightSpecialTypeCompatible)
                    {
                        if (!isRightOrdinaryTypeCompatible)
                        {
                            expressionType = UpdateVariableToUnambiguousSpecialType(expressionType);
                            expressionTypingResult = expressionTypingResult.WithType(expressionType);
                        }
                    }
                    else
                    {
                        if (isRightOrdinaryTypeCompatible)
                        {
                            expressionType = UpdateVariableToUnambiguousOrdinaryType(expressionType);
                            expressionTypingResult = expressionTypingResult.WithType(expressionType);
                        }
                        else
                        {
                            _diagnostics.Add(ErrorCode.ERR_UnsafeValueReturnedByDecoratorMethod, node.ExpressionOpt.Syntax.Location);
                            expressionTypingResult = expressionTypingResult.WithIsSuccessful(false);
                        }
                    }
                }
                else
                {
                    // If the right expression has an unambiguous type, it needs to be assignable to the specified decorated method's return type
                    if (!CheckSpecialTypeIsAssignableFrom(returnValueType, expressionType, subtypingAssertions))
                    {
                        _diagnostics.Add(ErrorCode.ERR_UnsafeValueReturnedByDecoratorMethod, node.ExpressionOpt.Syntax.Location);
                        expressionTypingResult = expressionTypingResult.WithIsSuccessful(false);
                    }
                }
            }

            return new DecoratorTypingResult(expressionTypingResult.IsSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitSequence(BoundSequence node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitSequencePoint(BoundSequencePoint node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitSequencePointExpression(BoundSequencePointExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult expressionTypingResult = Visit(node.Expression, subtypingAssertions);
            return new DecoratorTypingResult(expressionTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), expressionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitSequencePointWithSpan(BoundSequencePointWithSpan node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitSizeOfOperator(BoundSizeOfOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitStackAllocArrayCreation(BoundStackAllocArrayCreation node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult countTypingResult = Visit(node.Count, subtypingAssertions);
            return new DecoratorTypingResult(countTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), countTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitStateMachineScope(BoundStateMachineScope node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitStatementList(BoundStatementList node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitList(node.Statements, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitStringInsert(BoundStringInsert node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult valueTypingResult = Visit(node.Value, subtypingAssertions);
            DecoratorTypingResult alignmentTypingResult = Visit(node.Alignment, valueTypingResult.UpdatedSubtypingAssertions);
            DecoratorTypingResult formatTypingResult = Visit(node.Format, alignmentTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(
                valueTypingResult.IsSuccessful && alignmentTypingResult.IsSuccessful && formatTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                formatTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitSwitchLabel(BoundSwitchLabel node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression expressionOpt = node.ExpressionOpt;
            if (expressionOpt != null)
            {
                DecoratorTypingResult expressionTypingResult = VisitAndCleanUpInvalidatedLocals(expressionOpt, subtypingAssertions, DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation);
                isSuccessful &= expressionTypingResult.IsSuccessful;
            }
            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitSwitchSection(BoundSwitchSection node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = VisitList(node.BoundSwitchLabels, ref subtypingAssertions);
            isSuccessful &= VisitList(node.Statements, ref subtypingAssertions);
            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitSwitchStatement(BoundSwitchStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult boundExpressionTypingResult = VisitAndCleanUpInvalidatedLocals(node.BoundExpression, subtypingAssertions);
            bool isSuccessful = boundExpressionTypingResult.IsSuccessful;

            ImmutableHashSet<SubtypingAssertion> preSectionSubtypingAssertions = boundExpressionTypingResult.UpdatedSubtypingAssertions;
            ImmutableHashSet<SubtypingAssertion> postSectionSubtypingAssertions, nextPreSectionSubtypingAssertions;
            bool areSubtypingAssertionsStable;
            do
            {
                postSectionSubtypingAssertions = preSectionSubtypingAssertions;
                for (int i = 0; i < node.SwitchSections.Length; i++)
                {
                    DecoratorTypingResult switchSectionTypingResult = Visit(node.SwitchSections[i], preSectionSubtypingAssertions);
                    isSuccessful &= switchSectionTypingResult.IsSuccessful;
                    postSectionSubtypingAssertions = postSectionSubtypingAssertions.Intersect(switchSectionTypingResult.UpdatedSubtypingAssertions);
                }

                nextPreSectionSubtypingAssertions = boundExpressionTypingResult.UpdatedSubtypingAssertions.Intersect(postSectionSubtypingAssertions);
                areSubtypingAssertionsStable = preSectionSubtypingAssertions.SetEquals(nextPreSectionSubtypingAssertions);
                preSectionSubtypingAssertions = nextPreSectionSubtypingAssertions;
            }
            while (!areSubtypingAssertionsStable);

            return new DecoratorTypingResult(isSuccessful, null, postSectionSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitThisReference(BoundThisReference node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // References to the decorator object would be meaningless in the target method's context
            _diagnostics.Add(ErrorCode.ERR_ThisReferenceInDecorator, node.Syntax.Location);
            return new DecoratorTypingResult(false, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitThrowStatement(BoundThrowStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundExpression expressionOpt = node.ExpressionOpt;
            if (expressionOpt != null)
            {
                DecoratorTypingResult expressionTypingResult = VisitAndCleanUpInvalidatedLocals(expressionOpt, subtypingAssertions);
                isSuccessful &= expressionTypingResult.IsSuccessful;
                subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;
            }
            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitTryStatement(BoundTryStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult tryBlockTypingResult = Visit(node.TryBlock, subtypingAssertions);
            bool isSuccessful = tryBlockTypingResult.IsSuccessful;

            ImmutableHashSet<SubtypingAssertion> preCatchSubtypingAssertions = tryBlockTypingResult.UpdatedSubtypingAssertions;
            subtypingAssertions = preCatchSubtypingAssertions;
            for (int i = 0; i < node.CatchBlocks.Length; i++)
            {
                DecoratorTypingResult catchBlockTypingResult = Visit(node.CatchBlocks[i], preCatchSubtypingAssertions);
                isSuccessful &= catchBlockTypingResult.IsSuccessful;
                subtypingAssertions = subtypingAssertions.Intersect(catchBlockTypingResult.UpdatedSubtypingAssertions);
            }

            BoundBlock finallyBlockOpt = node.FinallyBlockOpt;
            if (finallyBlockOpt != null)
            {
                DecoratorTypingResult finallyBlockTypingResult = Visit(finallyBlockOpt, subtypingAssertions);
                isSuccessful &= finallyBlockTypingResult.IsSuccessful;
                subtypingAssertions = finallyBlockTypingResult.UpdatedSubtypingAssertions;
            }

            return new DecoratorTypingResult(isSuccessful, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitTypeExpression(BoundTypeExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitTypeOfOperator(BoundTypeOfOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            return new DecoratorTypingResult(true, new ExtendedTypeInfo(node.Type), subtypingAssertions);
        }

        public override DecoratorTypingResult VisitTypeOrInstanceInitializers(BoundTypeOrInstanceInitializers node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // Such nodes should only exist after lowering of the original source code
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitTypeOrValueExpression(BoundTypeOrValueExpression node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult typeExpressionTypingResult = Visit(node.Data.TypeExpression, subtypingAssertions);
            DecoratorTypingResult valueExpressionTypingResult = Visit(node.Data.ValueExpression, typeExpressionTypingResult.UpdatedSubtypingAssertions);
            return new DecoratorTypingResult(
                typeExpressionTypingResult.IsSuccessful && valueExpressionTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                valueExpressionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitUnaryOperator(BoundUnaryOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult operandTypingResult = Visit(node.Operand, subtypingAssertions);

            if (node.Type.SpecialType == SpecialType.System_Boolean)
            {
                // Boolean-valued unary operator expressions might be conditionals that introduce subtyping assertions, so we handle them separately
                ImmutableHashSet<SubtypingAssertion> assertionsIfTrue = null;
                ImmutableHashSet<SubtypingAssertion> assertionsIfFalse = null;
                switch (node.OperatorKind & UnaryOperatorKind.OpMask)
                {
                    case UnaryOperatorKind.LogicalNegation:
                        assertionsIfTrue = operandTypingResult.AssertionsIfFalse;
                        assertionsIfFalse = operandTypingResult.AssertionsIfTrue;
                        break;
                }

                return new DecoratorTypingResult(
                    operandTypingResult.IsSuccessful,
                    new ExtendedTypeInfo(node.Type),
                    operandTypingResult.UpdatedSubtypingAssertions,
                    assertionsIfTrue,
                    assertionsIfFalse);
            }
            else
            {
                return new DecoratorTypingResult(operandTypingResult.IsSuccessful, new ExtendedTypeInfo(node.Type), operandTypingResult.UpdatedSubtypingAssertions);
            }
        }

        public override DecoratorTypingResult VisitUnboundLambda(UnboundLambda node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // The decorator method's body should not contain unbound lambdas
            throw ExceptionUtilities.Unreachable;
        }

        public override DecoratorTypingResult VisitUserDefinedConditionalLogicalOperator(BoundUserDefinedConditionalLogicalOperator node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            BoundExpression left = node.Left;
            DecoratorTypingResult leftTypingResult = Visit(left, subtypingAssertions);
            if (!leftTypingResult.Type.IsOrdinaryType)
            {
                // We do not allow special-typed values in the branches of a conditional operator, as reasoning about their type-safety would be overly complicated
                leftTypingResult = UpdateResultOnIncompatibleSpecialType(leftTypingResult, ErrorCode.ERR_DecoratorMethodSpecialTypeInConditionalOperator, left);
            }

            BoundExpression right = node.Right;
            DecoratorTypingResult rightTypingResult = Visit(right, subtypingAssertions);
            if (!rightTypingResult.Type.IsOrdinaryType)
            {
                // We do not allow special-typed values in the branches of a conditional operator, as reasoning about their type-safety would be overly complicated
                rightTypingResult = UpdateResultOnIncompatibleSpecialType(rightTypingResult, ErrorCode.ERR_DecoratorMethodSpecialTypeInConditionalOperator, right);
            }

            return new DecoratorTypingResult(
                leftTypingResult.IsSuccessful && rightTypingResult.IsSuccessful,
                new ExtendedTypeInfo(node.Type),
                leftTypingResult.UpdatedSubtypingAssertions.Intersect(rightTypingResult.UpdatedSubtypingAssertions));
        }

        public override DecoratorTypingResult VisitUsingStatement(BoundUsingStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            BoundMultipleLocalDeclarations declarationsOpt = node.DeclarationsOpt;
            if (declarationsOpt != null)
            {
                DecoratorTypingResult declarationsTypingResult = VisitWithExtraFlags(DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation, declarationsOpt, subtypingAssertions);
                isSuccessful &= declarationsTypingResult.IsSuccessful;
                subtypingAssertions = declarationsTypingResult.UpdatedSubtypingAssertions;
            }

            BoundExpression expressionOpt = node.ExpressionOpt;
            if (expressionOpt != null)
            {
                DecoratorTypingResult expressionTypingResult = VisitAndCleanUpInvalidatedLocals(expressionOpt, subtypingAssertions, DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation);
                isSuccessful &= expressionTypingResult.IsSuccessful;
                subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;
            }

            DecoratorTypingResult bodyTypingResult = Visit(node.Body, subtypingAssertions);

            return new DecoratorTypingResult(isSuccessful && bodyTypingResult.IsSuccessful, null, bodyTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitWhileStatement(BoundWhileStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorTypingResult conditionTypingResult, bodyTypingResult;
            bool areSubtypingAssertionsStable;
            bool isSuccessful = true;
            ImmutableHashSet<SubtypingAssertion> iterationSubtypingAssertions = subtypingAssertions;
            do
            {
                conditionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Condition, iterationSubtypingAssertions, DecoratorMethodTypeCheckerFlags.ProhibitSpliceLocation);
                isSuccessful &= conditionTypingResult.IsSuccessful;

                bodyTypingResult = Visit(
                    node.Body,
                    ExtendSubtypingAssertions(conditionTypingResult.UpdatedSubtypingAssertions, conditionTypingResult.AssertionsIfTrue));
                isSuccessful &= bodyTypingResult.IsSuccessful;

                ImmutableHashSet<SubtypingAssertion> nextIterationSubtypingAssertions = subtypingAssertions.Intersect(bodyTypingResult.UpdatedSubtypingAssertions);
                areSubtypingAssertionsStable = iterationSubtypingAssertions.SetEquals(nextIterationSubtypingAssertions);
                iterationSubtypingAssertions = nextIterationSubtypingAssertions;
            }
            while (!areSubtypingAssertionsStable);

            // We do not add the assertions if condition is false to the final set of subtyping assertions, as we want to preserve the useful property that it is always a subset of the
            // initial subtyping assertions. This is necessary for proper propagation of guaranteed subtyping assertions to catch/finally blocks, where an exception might terminate the execution
            // of a statement in the corresponding try block prematurely
            return new DecoratorTypingResult(isSuccessful, null, conditionTypingResult.UpdatedSubtypingAssertions);
        }

        public override DecoratorTypingResult VisitYieldBreakStatement(BoundYieldBreakStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_BadYieldInDecoratorMethod, node.Syntax.Location);
            return new DecoratorTypingResult(false, null, subtypingAssertions);
        }

        public override DecoratorTypingResult VisitYieldReturnStatement(BoundYieldReturnStatement node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            _diagnostics.Add(ErrorCode.ERR_BadYieldInDecoratorMethod, node.Syntax.Location);
            DecoratorTypingResult expressionTypingResult = VisitAndCleanUpInvalidatedLocals(node.Expression, subtypingAssertions);
            return new DecoratorTypingResult(false, null, expressionTypingResult.UpdatedSubtypingAssertions);
        }

        public DecoratorTypingResult VisitWithExtraFlags(DecoratorMethodTypeCheckerFlags extraFlags, BoundNode node, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            DecoratorMethodTypeCheckerFlags oldFlags = _flags;
            _flags |= extraFlags;

            DecoratorTypingResult typingResult = Visit(node, subtypingAssertions);

            _flags = oldFlags;

            return typingResult;
        }

        public DecoratorTypingResult VisitAndCleanUpInvalidatedLocals(
            BoundExpression node,
            ImmutableHashSet<SubtypingAssertion> subtypingAssertions,
            DecoratorMethodTypeCheckerFlags extraFlags = DecoratorMethodTypeCheckerFlags.None)

        {
            DecoratorTypingResult typingResult = VisitWithExtraFlags(extraFlags, node, subtypingAssertions);
            typingResult = InvalidateConditionalSubtypingAssertions(typingResult, _invalidatedLocals);
            _invalidatedLocals = ImmutableHashSet<LocalSymbol>.Empty;
            return typingResult;
        }

        public bool VisitArguments(
            IEnumerable<Symbol> applicableMethodsOrProperties,
            ImmutableArray<BoundExpression> arguments,
            ImmutableArray<RefKind> argumentRefKindsOpt,
            ref ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            foreach (Symbol methodOrProperty in applicableMethodsOrProperties)
            {
                isSuccessful &= VisitArguments(methodOrProperty, arguments, argumentRefKindsOpt, ref subtypingAssertions);
            }
            return isSuccessful;
        }

        public bool VisitList<T>(ImmutableArray<T> list, ref ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
            where T : BoundNode
        {
            bool isSuccessful = true;
            if (!list.IsDefaultOrEmpty)
            {
                for (int i = 0; i < list.Length; i++)
                {
                    DecoratorTypingResult typingResult = Visit(list[i], subtypingAssertions);
                    isSuccessful &= typingResult.IsSuccessful;
                    subtypingAssertions = typingResult.UpdatedSubtypingAssertions;
                }
            }
            return isSuccessful;
        }

        public bool VisitArguments(
            Symbol methodOrProperty,
            ImmutableArray<BoundExpression> arguments,
            ImmutableArray<RefKind> argumentRefKindsOpt,
            ref ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            Debug.Assert(methodOrProperty == null || methodOrProperty is MethodSymbol || methodOrProperty is PropertySymbol);

            ImmutableArray<TypeSymbol> parameterTypes;
            if (methodOrProperty == null)
            {
                parameterTypes = Enumerable.Repeat<TypeSymbol>(_compilation.ObjectType, arguments.Length).ToImmutableArray();
            }
            else if (methodOrProperty is MethodSymbol)
            {
                parameterTypes = ((MethodSymbol)methodOrProperty).ParameterTypes;
            }
            else if (methodOrProperty is PropertySymbol)
            {
                parameterTypes = ((PropertySymbol)methodOrProperty).ParameterTypes;
            }

            bool isSuccessful = true;
            for (int i = 0; i < arguments.Length; i++)
            {
                BoundExpression argument = arguments[i];
                DecoratorTypingResult argumentTypingResult = Visit(argument, subtypingAssertions);

                subtypingAssertions = argumentTypingResult.UpdatedSubtypingAssertions;

                // If the argument is passed as a ref or out argument, it should not be any of the decorator method parameters, and if it is a local, it should be invalidated
                RefKind argumentRefKind = argumentRefKindsOpt.IsDefault
                                            ? RefKind.None
                                            : argumentRefKindsOpt[i];
                if (argumentRefKind != RefKind.None)
                {
                    switch (argument.Kind)
                    {
                        case BoundKind.Parameter:
                            if (_decoratorMethod.Parameters.Contains(((BoundParameter)argument).ParameterSymbol))
                            {
                                _diagnostics.Add(ErrorCode.ERR_DecoratorMethodParameterModification, argument.Syntax.Location);
                                argumentTypingResult = argumentTypingResult.WithIsSuccessful(false);
                            }
                            break;

                        case BoundKind.Local:
                            AddInvalidatedLocal(((BoundLocal)argument).LocalSymbol, ref subtypingAssertions);
                            break;
                    }
                }

                ExtendedTypeInfo argumentType = argumentTypingResult.Type;
                if (!argumentType.IsOrdinaryType)
                {
                    switch (argumentType.Kind)
                    {
                        case ExtendedTypeKind.ThisObject:
                            if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(parameterTypes[i]), argumentType, subtypingAssertions))
                            {
                                argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodThisObjectCast, argument);
                            }
                            break;

                        case ExtendedTypeKind.ArgumentArray:
                            // Argument arrays cannot be passed to arbitrary method or indexer calls
                            argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, argument);
                            break;

                        case ExtendedTypeKind.Parameter:
                            // Decorated method argument expressions should always be indexings of an argument array, so they cannot be passed as ref or out arguments according to C#'s syntax
                            Debug.Assert(argumentRefKind == RefKind.None);
                            break;

                        case ExtendedTypeKind.ReturnValue:
                            if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(parameterTypes[i]), argumentType, subtypingAssertions))
                            {
                                argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodReturnValueCast, argument);
                            }

                            if (argumentRefKind != RefKind.None)
                            {
                                // If a return value variable is used as a ref or out argument, the call parameter's type needs to be assignable to the decorated method's return type
                                if (!CheckSpecialTypeIsAssignableFrom(argumentType, new ExtendedTypeInfo(parameterTypes[i]), subtypingAssertions))
                                {
                                    argumentTypingResult = UpdateResultOnIncompatibleSpecialType(argumentTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodReturnValueRefParameterUse, argument);
                                }
                            }
                            break;
                    }
                }
                isSuccessful &= argumentTypingResult.IsSuccessful;
            }
            return isSuccessful;
        }

        public bool VisitExpressions(ImmutableArray<BoundExpression> expressions, ref ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            bool isSuccessful = true;
            for (int i = 0; i < expressions.Length; i++)
            {
                BoundExpression expression = expressions[i];
                DecoratorTypingResult expressionTypingResult = Visit(expression, subtypingAssertions);

                subtypingAssertions = expressionTypingResult.UpdatedSubtypingAssertions;

                ExtendedTypeInfo argumentType = expressionTypingResult.Type;
                if (!argumentType.IsOrdinaryType)
                {
                    switch (argumentType.Kind)
                    {
                        case ExtendedTypeKind.ThisObject:
                            // Ensure that a this-reference is assignable to the expression's ordinary type
                            if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(expression.Type), argumentType, subtypingAssertions))
                            {
                                expressionTypingResult = UpdateResultOnIncompatibleSpecialType(expressionTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodThisObjectCast, expression);
                            }
                            break;

                        case ExtendedTypeKind.ArgumentArray:
                            // Argument arrays cannot be passed to arbitrary method calls
                            expressionTypingResult = UpdateResultOnIncompatibleSpecialType(expressionTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayUse, expression);
                            break;

                        case ExtendedTypeKind.ReturnValue:
                            // Ensure that a return value is assignable to the expression's ordinary type
                            if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(expression.Type), argumentType, subtypingAssertions))
                            {
                                expressionTypingResult = UpdateResultOnIncompatibleSpecialType(expressionTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodReturnValueCast, expression);
                            }
                            break;
                    }
                }
                isSuccessful &= expressionTypingResult.IsSuccessful;
            }
            return isSuccessful;
        }

        private static ImmutableHashSet<SubtypingAssertion> EmptyAssertions
        {
            get { return ImmutableHashSet.Create<SubtypingAssertion>(SubtypingAssertionComparer.Singleton); }
        }

        private static ImmutableHashSet<SubtypingAssertion> InvalidateSubtypingAssertions(
            ImmutableHashSet<SubtypingAssertion> originalSubtypingAssertions,
            LocalSymbol invalidatedLocal)
        {
            if (originalSubtypingAssertions.IsEmpty)
            {
                return originalSubtypingAssertions;
            }

            var builder = ImmutableHashSet.CreateBuilder(SubtypingAssertionComparer.Singleton);
            foreach (SubtypingAssertion subtypingAssertion in originalSubtypingAssertions)
            {
                if ((subtypingAssertion.Supertype.Kind != ExtendedTypeKind.Parameter
                     || subtypingAssertion.Supertype.ParameterIndexLocal != invalidatedLocal)
                    && (subtypingAssertion.Subtype.Kind == ExtendedTypeKind.Parameter
                        || subtypingAssertion.Subtype.ParameterIndexLocal != invalidatedLocal))
                {
                    // This subtyping assertion is not affected by the invalidation of the local variable
                    builder.Add(subtypingAssertion);
                }
            }
            return builder.Count == originalSubtypingAssertions.Count ? originalSubtypingAssertions : builder.ToImmutable();
        }

        private static ImmutableHashSet<SubtypingAssertion> InvalidateSubtypingAssertions(
            ImmutableHashSet<SubtypingAssertion> originalSubtypingAssertions,
            ImmutableHashSet<LocalSymbol> invalidatedLocals)
        {
            if (originalSubtypingAssertions.IsEmpty || invalidatedLocals.Count == 0)
            {
                return originalSubtypingAssertions;
            }

            var builder = ImmutableHashSet.CreateBuilder(SubtypingAssertionComparer.Singleton);
            foreach (SubtypingAssertion subtypingAssertion in originalSubtypingAssertions)
            {
                if ((subtypingAssertion.Supertype.Kind != ExtendedTypeKind.Parameter
                     || !invalidatedLocals.Contains(subtypingAssertion.Supertype.ParameterIndexLocal))
                    && (subtypingAssertion.Subtype.Kind == ExtendedTypeKind.Parameter
                        || !invalidatedLocals.Contains(subtypingAssertion.Subtype.ParameterIndexLocal)))
                {
                    // This subtyping assertion is not affected by the invalidation of any local variable
                    builder.Add(subtypingAssertion);
                }
            }
            return builder.Count == originalSubtypingAssertions.Count ? originalSubtypingAssertions : builder.ToImmutable();
        }

        private static DecoratorTypingResult InvalidateConditionalSubtypingAssertions(DecoratorTypingResult originalTypingResult, ImmutableHashSet<LocalSymbol> invalidatedLocals)
        {
            if (!originalTypingResult.Type.IsOrdinaryType || originalTypingResult.Type.OrdinaryType.SpecialType != SpecialType.System_Boolean)
            {
                Debug.Assert(originalTypingResult.AssertionsIfTrue == null && originalTypingResult.AssertionsIfFalse == null);
                return originalTypingResult;
            }

            ImmutableHashSet<SubtypingAssertion> updatedAssertionsIfTrue = null;
            if (originalTypingResult.AssertionsIfTrue != null)
            {
                updatedAssertionsIfTrue = InvalidateSubtypingAssertions(originalTypingResult.AssertionsIfTrue, invalidatedLocals);
            }

            ImmutableHashSet<SubtypingAssertion> updatedAssertionsIfFalse = null;
            if (originalTypingResult.AssertionsIfFalse != null)
            {
                updatedAssertionsIfFalse = InvalidateSubtypingAssertions(originalTypingResult.AssertionsIfFalse, invalidatedLocals);
            }

            if (updatedAssertionsIfTrue != originalTypingResult.AssertionsIfTrue || updatedAssertionsIfFalse != originalTypingResult.AssertionsIfFalse)
            {
                return new DecoratorTypingResult(
                    originalTypingResult.IsSuccessful,
                    originalTypingResult.Type,
                    originalTypingResult.UpdatedSubtypingAssertions,
                    updatedAssertionsIfTrue,
                    updatedAssertionsIfFalse);
            }
            else
            {
                return originalTypingResult;
            }
        }

        private static bool CheckSpecialTypeIsAssignableTo(ExtendedTypeInfo targetType, ExtendedTypeInfo sourceType, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            Debug.Assert(sourceType.Kind != ExtendedTypeKind.OrdinaryType);

            // Argument values can always be assigned to object
            if (sourceType.Kind == ExtendedTypeKind.Parameter
                && targetType.IsOrdinaryType
                && targetType.OrdinaryType.IsObjectType())
            {
                return true;
            }

            // If the special types coincide, they are trivially assignable to each other
            if (targetType.MatchesSpecialType(sourceType))
            {
                return true;
            }

            foreach (SubtypingAssertion subtypingAssertion in subtypingAssertions)
            {
                if (subtypingAssertion.Subtype.MatchesSpecialType(sourceType))
                {
                    if (targetType.Kind == ExtendedTypeKind.OrdinaryType
                        && subtypingAssertion.Supertype.Kind == ExtendedTypeKind.OrdinaryType)
                    {
                        if (MetaUtils.CheckTypeIsAssignableFrom(targetType.OrdinaryType, subtypingAssertion.Supertype.OrdinaryType))
                        {
                            return true;
                        }
                    }
                    else if (targetType.Kind != ExtendedTypeKind.OrdinaryType
                             && subtypingAssertion.Supertype.MatchesSpecialType(targetType))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool CheckSpecialTypeIsAssignableFrom(ExtendedTypeInfo targetType, ExtendedTypeInfo sourceType, ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            Debug.Assert(targetType.Kind != ExtendedTypeKind.OrdinaryType);

            // If the special types coincide, they are trivially assignable to each other
            if (sourceType.MatchesSpecialType(targetType))
            {
                return true;
            }

            foreach (SubtypingAssertion subtypingAssertion in subtypingAssertions)
            {
                if (subtypingAssertion.Supertype.MatchesSpecialType(targetType))
                {
                    if (sourceType.Kind == ExtendedTypeKind.OrdinaryType
                        && subtypingAssertion.Subtype.Kind == ExtendedTypeKind.OrdinaryType)
                    {
                        if (MetaUtils.CheckTypeIsAssignableFrom(subtypingAssertion.Subtype.OrdinaryType, sourceType.OrdinaryType))
                        {
                            return true;
                        }
                    }
                    else if (sourceType.Kind != ExtendedTypeKind.OrdinaryType
                             && subtypingAssertion.Subtype.MatchesSpecialType(sourceType))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool CheckIsSpecificParameter(BoundExpression node, ParameterSymbol parameter)
        {
            while (node.Kind == BoundKind.Conversion)
            {
                node = ((BoundConversion)node).Operand;
            }
            return node.Kind == BoundKind.Parameter && ((BoundParameter)node).ParameterSymbol == parameter;
        }

        private bool CheckIsSpliceLocation(BoundCall call)
        {
            MethodSymbol method = call.Method;
            if (call.Method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__Invoke))
            {
                // This is a call to MethodBase.Invoke(object obj, object[] parameters)
                if (call.ReceiverOpt != null
                    && CheckIsSpecificParameter(call.ReceiverOpt, _decoratorMethod.Parameters[0])
                    && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[1])
                    && (call.Arguments[1].Kind == BoundKind.Parameter || call.Arguments[1].Kind == BoundKind.Local))
                {
                    return true;
                }
                else
                {
                    // Disallow calls to MethodBase.Invoke(object obj, object[] parameters) which are not obvious splices
                    // (as they might use a different thisObject, or they might refer to this method through a different local variable, leading to infinite recursion)
                    _diagnostics.Add(ErrorCode.ERR_InvalidInvokeInDecorator, call.Syntax.Location);
                }
            }
            return false;
        }

        private bool CheckIsBaseDecoratorMethodCall(BoundCall call)
        {
            MethodSymbol method = call.Method;
            if (method.Name != _decoratorMethod.Name
                || method.ParameterCount != _decoratorMethod.ParameterCount)
            {
                return false;
            }

            for (int i = 0; i < method.ParameterCount; i++)
            {
                ParameterSymbol methodParameter = method.Parameters[i];
                ParameterSymbol decoratorMethodParameter = _decoratorMethod.Parameters[i];
                if (methodParameter.Type != decoratorMethodParameter.Type
                    || methodParameter.RefKind != decoratorMethodParameter.RefKind)
                {
                    return false;
                }
            }

            BoundExpression receiverOpt = call.ReceiverOpt;
            return receiverOpt != null && receiverOpt.Kind == BoundKind.BaseReference;
        }

        private ExtendedTypeInfo TryParseTypeFromExpression(BoundExpression node)
        {
            Debug.Assert(node.Type == _compilation.GetWellKnownType(WellKnownType.System_Type));

            switch (node.Kind)
            {
                case BoundKind.TypeOfOperator:
                    var typeOfOperator = (BoundTypeOfOperator)node;
                    return new ExtendedTypeInfo(typeOfOperator.SourceType.Type);

                case BoundKind.Call:
                    var call = (BoundCall)node;
                    MethodSymbol method = call.Method;
                    if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType)
                        && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[0]))
                    {
                        return ExtendedTypeInfo.CreateThisObjectType(_compilation);
                    }
                    else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType)
                        && CheckIsSpecificParameter(call.Arguments[0], _decoratorMethod.Parameters[0])
                        && call.Arguments[1].Kind == BoundKind.Local)
                    {
                        return ExtendedTypeInfo.CreateParameterType(_compilation, ((BoundLocal)call.Arguments[1]).LocalSymbol);
                    }
                    break;

                case BoundKind.PropertyAccess:
                    var propertyAccess = (BoundPropertyAccess)node;
                    BoundExpression receiverOpt = propertyAccess.ReceiverOpt;
                    if (propertyAccess.PropertySymbol == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType)
                        && receiverOpt != null
                        && CheckIsSpecificParameter(receiverOpt, _decoratorMethod.Parameters[0]))
                    {
                        return ExtendedTypeInfo.CreateReturnValueType(_compilation, false);
                    }
                    break;
            }

            return null;
        }

        private bool AddInvalidatedLocal(LocalSymbol local, ref ImmutableHashSet<SubtypingAssertion> subtypingAssertions)
        {
            // If the local was already in the set, we do not need to invalidate any more subtyping assertions,
            // as subtyping assertions are only added to the list on statement boundaries, when all invalidated locals are cleared as well
            ImmutableHashSet<LocalSymbol> newInvalidatedLocals = _invalidatedLocals.Add(local);
            if (_invalidatedLocals == newInvalidatedLocals)
            {
                return false;
            }
            else
            {
                _invalidatedLocals = newInvalidatedLocals;
                if (_flags.HasFlag(DecoratorMethodTypeCheckerFlags.InNestedLambdaBody)
                    && _outerScopeVariables.Contains(local))
                {
                    _blacklistedLocals = _blacklistedLocals.Add(local);
                }
                subtypingAssertions = InvalidateSubtypingAssertions(subtypingAssertions, local);
                return true;
            }
        }

        private ImmutableHashSet<SubtypingAssertion> ExtendSubtypingAssertions(
            ImmutableHashSet<SubtypingAssertion> originalSubtypingAssertions,
            ImmutableHashSet<SubtypingAssertion> extraSubtypingAssertions)
        {
            if (extraSubtypingAssertions == null || extraSubtypingAssertions.IsEmpty)
            {
                return originalSubtypingAssertions;
            }
            return originalSubtypingAssertions.Union(InvalidateSubtypingAssertions(extraSubtypingAssertions, _blacklistedLocals));
        }

        private void ValidateSpecialTypeAssignment(
            ref DecoratorTypingResult leftTypingResult,
            ref DecoratorTypingResult rightTypingResult,
            ImmutableHashSet<SubtypingAssertion> subtypingAssertions,
            BoundNode node)
        {
            ExtendedTypeInfo leftType = leftTypingResult.Type;

            if (!leftType.IsOrdinaryType)
            {
                ExtendedTypeInfo rightType = rightTypingResult.Type;
                // Check that the right expression can be assigned to the special type of the left expression
                switch (leftType.Kind)
                {
                    case ExtendedTypeKind.ArgumentArray:
                        if (rightType.IsOrdinaryType)
                        {
                            leftTypingResult = UpdateResultOnIncompatibleSpecialType(leftTypingResult, ErrorCode.ERR_OrdinaryArrayAssignedToDecoratedMethodArgumentArray, node);
                        }
                        else if (leftType.IsAmbiguous && !rightType.IsAmbiguous)
                        {
                            leftType = UpdateVariableToUnambiguousSpecialType(leftType);
                            leftTypingResult = leftTypingResult.WithType(leftType);
                        }
                        else if (!leftType.IsAmbiguous && rightType.IsAmbiguous)
                        {
                            rightType = UpdateVariableToUnambiguousSpecialType(rightType);
                            rightTypingResult = rightTypingResult.WithType(rightType);
                        }
                        break;

                    case ExtendedTypeKind.Parameter:
                        // Decorated method argument expressions should never be ambiguously-typed
                        Debug.Assert(!leftType.IsAmbiguous);

                        if (_invalidatedLocals.Contains(leftType.ParameterIndexLocal))
                        {
                            _diagnostics.Add(ErrorCode.ERR_UnsafeValueAssignedToDecoratedMethodArgument, node.Syntax.Location);
                            leftTypingResult = leftTypingResult.WithIsSuccessful(false);
                        }
                        else if (rightType.IsAmbiguous)
                        {
                            // If the right expression has an ambiguous type, it is enough for either one of the alternatives to be assignable to the decorated method's parameter type.
                            // We update it to an inambiguous type if one of the alternatives is rejected
                            ExtendedTypeInfo rightSpecialType = rightType.UpdateToUnambiguousSpecialType();
                            bool isRightSpecialTypeCompatible = CheckSpecialTypeIsAssignableFrom(leftType, rightSpecialType, subtypingAssertions);
                            ExtendedTypeInfo rightOrdinaryType = rightType.UpdateToUnambiguousOrdinaryType();
                            bool isRightOrdinaryTypeCompatible = CheckSpecialTypeIsAssignableFrom(leftType, rightOrdinaryType, subtypingAssertions);
                            if (isRightSpecialTypeCompatible)
                            {
                                if (!isRightOrdinaryTypeCompatible)
                                {
                                    rightType = UpdateVariableToUnambiguousSpecialType(rightType);
                                    rightTypingResult = rightTypingResult.WithType(rightType);
                                }
                            }
                            else
                            {
                                if (isRightOrdinaryTypeCompatible)
                                {
                                    rightType = UpdateVariableToUnambiguousOrdinaryType(rightType);
                                    rightTypingResult = rightTypingResult.WithType(rightType);
                                }
                                else
                                {
                                    _diagnostics.Add(ErrorCode.ERR_UnsafeValueAssignedToDecoratedMethodArgument, node.Syntax.Location);
                                    leftTypingResult = leftTypingResult.WithIsSuccessful(false);
                                }
                            }
                        }
                        else
                        {
                            // If the right expression has an unambiguous type, it needs to be assignable to the specified decorated method's parameter type
                            if (!CheckSpecialTypeIsAssignableFrom(leftType, rightType, subtypingAssertions))
                            {
                                _diagnostics.Add(ErrorCode.ERR_UnsafeValueAssignedToDecoratedMethodArgument, node.Syntax.Location);
                                leftTypingResult = leftTypingResult.WithIsSuccessful(false);
                            }
                        }
                        break;

                    case ExtendedTypeKind.ReturnValue:
                        if (rightType.IsAmbiguous)
                        {
                            // If the right expression has an ambiguous type, it is enough for either one of the alternatives to be assignable to the decorated method's return type.
                            // We update it to an inambiguous type if one of the alternatives is rejected
                            ExtendedTypeInfo rightSpecialType = rightType.UpdateToUnambiguousSpecialType();
                            bool isRightSpecialTypeCompatible = CheckSpecialTypeIsAssignableFrom(leftType, rightSpecialType, subtypingAssertions);
                            ExtendedTypeInfo rightOrdinaryType = rightType.UpdateToUnambiguousOrdinaryType();
                            bool isRightOrdinaryTypeCompatible = CheckSpecialTypeIsAssignableFrom(leftType, rightOrdinaryType, subtypingAssertions);
                            if (isRightSpecialTypeCompatible)
                            {
                                if (!isRightOrdinaryTypeCompatible)
                                {
                                    rightType = UpdateVariableToUnambiguousSpecialType(rightType);
                                    rightTypingResult = rightTypingResult.WithType(rightType);
                                }
                            }
                            else
                            {
                                if (isRightOrdinaryTypeCompatible)
                                {
                                    rightType = UpdateVariableToUnambiguousOrdinaryType(rightType);
                                    rightTypingResult = rightTypingResult.WithType(rightType);
                                }
                                else
                                {
                                    leftTypingResult = UpdateResultOnIncompatibleSpecialType(leftTypingResult, ErrorCode.ERR_UnsafeValueAssignedToDecoratedMethodReturnValue, node);
                                }
                            }
                        }
                        else
                        {
                            // If the right expression has an unambiguous type, it needs to be assignable to the specified decorated method's return type
                            if (!CheckSpecialTypeIsAssignableFrom(leftType, rightType, subtypingAssertions))
                            {
                                leftTypingResult = UpdateResultOnIncompatibleSpecialType(leftTypingResult, ErrorCode.ERR_UnsafeValueAssignedToDecoratedMethodReturnValue, node);
                            }

                            // If the left expression still has an ambiguous type and the right expression has a special unambiguous type, check if the left expression's ordinary type
                            // is compatible with the right expression's special type and remove the ambiguity if it isn't
                            if (leftType.IsAmbiguous && !rightType.IsOrdinaryType
                                && !CheckSpecialTypeIsAssignableTo(leftType.UpdateToUnambiguousOrdinaryType(), rightType, subtypingAssertions))
                            {
                                leftType = UpdateVariableToUnambiguousSpecialType(leftType);
                                leftTypingResult = leftTypingResult.WithType(leftType);
                            }
                        }
                        break;
                }
            }
        }

        private void ValidateSpecialTypeConversion(
            ref DecoratorTypingResult operandTypingResult,
            ImmutableHashSet<SubtypingAssertion> subtypingAssertions,
            BoundExpression node)
        {
            ExtendedTypeInfo operandType = operandTypingResult.Type;
            if (!operandType.IsOrdinaryType)
            {
                // Check that the special type of the operand expression can be safely cast to the target type
                switch (operandType.Kind)
                {
                    case ExtendedTypeKind.ThisObject:
                        // Decorated method this-reference expressions should never be ambiguously-typed
                        Debug.Assert(!operandType.IsAmbiguous);

                        if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(node.Type), operandType, subtypingAssertions))
                        {
                            _diagnostics.Add(ErrorCode.ERR_UnsafeDecoratedMethodThisObjectCast, node.Syntax.Location);
                            operandTypingResult = operandTypingResult.WithIsSuccessful(false);
                        }
                        break;

                    case ExtendedTypeKind.ArgumentArray:
                        // No conversions are allowed on decorated method argument array expressions
                        operandTypingResult = UpdateResultOnIncompatibleSpecialType(operandTypingResult, ErrorCode.ERR_InvalidDecoratedMethodArgumentArrayCast, node);
                        break;

                    case ExtendedTypeKind.Parameter:
                        // Decorated method argument expressions should never be ambiguously-typed
                        Debug.Assert(!operandType.IsAmbiguous);

                        if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(node.Type), operandType, subtypingAssertions))
                        {
                            _diagnostics.Add(ErrorCode.ERR_UnsafeDecoratedMethodArgumentCast, node.Syntax.Location);
                            operandTypingResult = operandTypingResult.WithIsSuccessful(false);
                        }
                        break;

                    case ExtendedTypeKind.ReturnValue:
                        if (!CheckSpecialTypeIsAssignableTo(new ExtendedTypeInfo(node.Type), operandType, subtypingAssertions))
                        {
                            operandTypingResult = UpdateResultOnIncompatibleSpecialType(operandTypingResult, ErrorCode.ERR_UnsafeDecoratedMethodReturnValueCast, node);
                        }
                        break;
                }
            }
        }

        private DecoratorTypingResult UpdateResultOnIncompatibleSpecialType(
            DecoratorTypingResult originalResult,
            ErrorCode errorCode,
            BoundNode node)
        {
            ExtendedTypeInfo type = originalResult.Type;
            if (type.IsAmbiguous)
            {
                type = UpdateVariableToUnambiguousOrdinaryType(type);
                return originalResult.WithType(type);
            }
            else
            {
                _diagnostics.Add(errorCode, node.Syntax.Location);
                return originalResult.WithIsSuccessful(false);
            }
        }

        private ExtendedTypeInfo UpdateVariableToUnambiguousOrdinaryType(ExtendedTypeInfo originalType)
        {
            ExtendedTypeInfo unambiguousType = originalType.UpdateToUnambiguousOrdinaryType();

            // Update the typing context to make the variable's type unambiguous
            _variableTypes = _variableTypes.SetItem(originalType.RootSymbol, unambiguousType);

            return unambiguousType;
        }

        private ExtendedTypeInfo UpdateVariableToUnambiguousSpecialType(ExtendedTypeInfo originalType)
        {
            ExtendedTypeInfo unambiguousType = originalType.UpdateToUnambiguousSpecialType();

            // Update the typing context to make the variable's type unambiguous
            _variableTypes = _variableTypes.SetItem(originalType.RootSymbol, unambiguousType);

            return unambiguousType;
        }
    }
}
