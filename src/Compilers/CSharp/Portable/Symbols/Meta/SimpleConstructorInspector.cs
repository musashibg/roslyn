// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Meta
{
    internal sealed class SimpleConstructorInspector : BoundTreeVisitor
    {
        private readonly DiagnosticBag _diagnostics;
        private readonly ImmutableDictionary<Symbol, SimpleConstructorAssignmentOperand>.Builder _memberAssignments;

        public SimpleConstructorInspector(DiagnosticBag diagnostics)
        {
            _diagnostics = diagnostics;
            _memberAssignments = ImmutableDictionary.CreateBuilder<Symbol, SimpleConstructorAssignmentOperand>();
        }

        public static void Inspect(SourceConstructorSymbol constructor, DiagnosticBag diagnostics)
        {
            BoundBlock constructorBody = constructor.EarlyBoundBody;
            Debug.Assert(constructorBody != null);

            var inspector = new SimpleConstructorInspector(diagnostics);
            try
            {
                inspector.Visit(constructorBody);

                constructor.SimpleConstructorAssignments = inspector._memberAssignments.ToImmutable();
            }
            catch (SimpleConstructorInspectionException)
            {
            }
        }

        public override BoundNode DefaultVisit(BoundNode node)
        {
            TerminateWithError(node);
            return null;
        }

        public override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node)
        {
            BoundExpression left = node.Left;
            Symbol leftSymbol = null;
            switch (left.Kind)
            {
                case BoundKind.FieldAccess:
                    var fieldAccessExpression = (BoundFieldAccess)left;
                    if (fieldAccessExpression.ReceiverOpt == null || fieldAccessExpression.ReceiverOpt.Kind != BoundKind.ThisReference)
                    {
                        TerminateWithError(node);
                    }
                    leftSymbol = fieldAccessExpression.FieldSymbol;
                    break;

                case BoundKind.PropertyAccess:
                    var propertyAccessExpression = (BoundPropertyAccess)left;
                    if (propertyAccessExpression.ReceiverOpt == null || propertyAccessExpression.ReceiverOpt.Kind != BoundKind.ThisReference)
                    {
                        TerminateWithError(node);
                    }
                    leftSymbol = propertyAccessExpression.PropertySymbol;
                    break;

                default:
                    TerminateWithError(node);
                    break;
            }

            _memberAssignments[leftSymbol] = GetAssignmentOperand(node.Right, node);
            return null;
        }

        public override BoundNode VisitBlock(BoundBlock node)
        {
            VisitList(node.Statements);
            return null;
        }

        public override BoundNode VisitCall(BoundCall node)
        {
            BoundExpression receiverOpt = node.ReceiverOpt;
            MethodSymbol method = node.Method;
            if (receiverOpt != null && (receiverOpt.Kind == BoundKind.BaseReference || receiverOpt.Kind == BoundKind.ThisReference)
                && method.MethodKind == MethodKind.Constructor)
            {
                if (method is SourceConstructorSymbol)
                {
                    var constructor = (SourceConstructorSymbol)method;
                    if (constructor.SimpleConstructorAssignments != null)
                    {
                        // Get the member assignments from the base constructor
                        foreach (KeyValuePair<Symbol, SimpleConstructorAssignmentOperand> kv in constructor.SimpleConstructorAssignments)
                        {
                            SimpleConstructorAssignmentOperand originalOperand = kv.Value;
                            SimpleConstructorAssignmentOperand newOperand;
                            if (originalOperand.Expression != null)
                            {
                                Debug.Assert(originalOperand.Parameter == null);
                                newOperand = kv.Value;
                            }
                            else
                            {
                                Debug.Assert(originalOperand.Parameter != null);
                                int parameterIndex = constructor.Parameters.IndexOf(originalOperand.Parameter);
                                Debug.Assert(parameterIndex >= 0 && parameterIndex < node.Arguments.Length);
                                newOperand = GetAssignmentOperand(node.Arguments[parameterIndex], node);
                            }
                            _memberAssignments[kv.Key] = newOperand;
                        }
                    }
                }
            }
            else
            {
                TerminateWithError(node);
            }
            return null;
        }

        public override BoundNode VisitExpressionStatement(BoundExpressionStatement node)
        {
            Visit(node.Expression);
            return null;
        }

        public override BoundNode VisitNoOpStatement(BoundNoOpStatement node)
        {
            return null;
        }

        public override BoundNode VisitStatementList(BoundStatementList node)
        {
            VisitList(node.Statements);
            return null;
        }

        private void VisitList<T>(ImmutableArray<T> list)
            where T : BoundNode
        {
            if (!list.IsDefaultOrEmpty)
            {
                for (int i = 0; i < list.Length; i++)
                {
                    Visit(list[i]);
                }
            }
        }

        private SimpleConstructorAssignmentOperand GetAssignmentOperand(BoundExpression expression, BoundNode contextNode)
        {
            if (expression.ConstantValue != null)
            {
                return new SimpleConstructorAssignmentOperand(expression);
            }

            Symbol symbol = null;
            switch (expression.Kind)
            {
                case BoundKind.Parameter:
                    symbol = ((BoundParameter)expression).ParameterSymbol;
                    break;

                case BoundKind.FieldAccess:
                    var fieldAccessExpression = (BoundFieldAccess)expression;
                    if (fieldAccessExpression.ReceiverOpt == null || fieldAccessExpression.ReceiverOpt.Kind != BoundKind.ThisReference)
                    {
                        TerminateWithError(contextNode);
                    }
                    symbol = fieldAccessExpression.FieldSymbol;
                    break;

                case BoundKind.PropertyAccess:
                    var propertyAccessExpression = (BoundPropertyAccess)expression;
                    if (propertyAccessExpression.ReceiverOpt == null || propertyAccessExpression.ReceiverOpt.Kind != BoundKind.ThisReference)
                    {
                        TerminateWithError(contextNode);
                    }
                    symbol = propertyAccessExpression.PropertySymbol;
                    break;

                default:
                    TerminateWithError(contextNode);
                    break;
            }

            if (symbol.Kind == SymbolKind.Parameter)
            {
                return new SimpleConstructorAssignmentOperand((ParameterSymbol)symbol);
            }
            else if (_memberAssignments.ContainsKey(symbol))
            {
                return _memberAssignments[symbol];
            }
            else
            {
                return new SimpleConstructorAssignmentOperand(
                    new BoundDefaultOperator(expression.Syntax, expression.Type) { WasCompilerGenerated = true });
            }
        }

        private void TerminateWithError(BoundNode node)
        {
            _diagnostics.Add(ErrorCode.ERR_InvalidDecoratorOrMetaclassConstructorBody, node.Syntax.Location);
            throw new SimpleConstructorInspectionException();
        }

        protected sealed override BoundExpression VisitExpressionWithoutStackGuard(BoundExpression node)
        {
            return (BoundExpression)Visit(node);
        }
    }
}
