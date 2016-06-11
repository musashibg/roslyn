using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    internal sealed class DecoratorInstanceArgumentRewriter : BoundTreeRewriter
    {
        private readonly CSharpCompilation _compilation;
        private readonly SourceMemberMethodSymbol _applicationMethod;
        private readonly ImmutableDictionary<Symbol, BoundExpression> _metaclassArguments;
        private readonly IReadOnlyDictionary<Symbol, CompileTimeValue> _variableValues;
        private readonly DiagnosticBag _diagnostics;

        public DecoratorInstanceArgumentRewriter(
            CSharpCompilation compilation,
            SourceMemberMethodSymbol applicationMethod,
            ImmutableDictionary<Symbol, BoundExpression> metaclassArguments,
            IReadOnlyDictionary<Symbol, CompileTimeValue> variableValues,
            DiagnosticBag diagnostics)
        {
            _compilation = compilation;
            _applicationMethod = applicationMethod;
            _metaclassArguments = metaclassArguments;
            _variableValues = variableValues;
            _compilation = compilation;
        }

        public override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node)
        {
            _diagnostics.Add(ErrorCode.ERR_BadExpressionInDecoratorArgument, node.Syntax.Location);
            return MakeBadExpression(node.Syntax, node.Type);
        }

        public override BoundNode VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node)
        {
            _diagnostics.Add(ErrorCode.ERR_BadExpressionInDecoratorArgument, node.Syntax.Location);
            return MakeBadExpression(node.Syntax, node.Type);
        }

        public override BoundNode VisitFieldAccess(BoundFieldAccess node)
        {
            BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
            if (strippedReceiver != null && (strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
            {
                return VisitMetaclassArgument(node, node.FieldSymbol);
            }

            return base.VisitFieldAccess(node);
        }

        public override BoundNode VisitLocal(BoundLocal node)
        {
            LocalSymbol local = node.LocalSymbol;
            Debug.Assert(_variableValues.ContainsKey(local));

            CompileTimeValue value = _variableValues[local];
            if (value.Kind == CompileTimeValueKind.Simple)
            {
                return MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
            }
            else
            {
                _diagnostics.Add(ErrorCode.ERR_BadExpressionInDecoratorArgument, node.Syntax.Location);
                return MakeBadExpression(node.Syntax, node.Type);
            }
        }

        public override BoundNode VisitIncrementOperator(BoundIncrementOperator node)
        {
            _diagnostics.Add(ErrorCode.ERR_BadExpressionInDecoratorArgument, node.Syntax.Location);
            return MakeBadExpression(node.Syntax, node.Type);
        }

        public override BoundNode VisitParameter(BoundParameter node)
        {
            ParameterSymbol parameter = node.ParameterSymbol;
            if (_applicationMethod.Parameters.Contains(parameter))
            {
                CompileTimeValue value = _variableValues[parameter];
                Debug.Assert(value.Kind == CompileTimeValueKind.Simple);
                return MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
            }
            else
            {
                // This must be a nested lambda parameter - keep it intact
                return node;
            }
        }

        public override BoundNode VisitPropertyAccess(BoundPropertyAccess node)
        {
            BoundExpression strippedReceiver = MetaUtils.StripConversions(node.ReceiverOpt);
            if (strippedReceiver != null && (strippedReceiver.Kind == BoundKind.BaseReference || strippedReceiver.Kind == BoundKind.ThisReference))
            {
                return VisitMetaclassArgument(node, node.PropertySymbol);
            }

            return base.VisitPropertyAccess(node);
        }

        protected override BoundExpression VisitExpressionWithoutStackGuard(BoundExpression node)
        {
            return (BoundExpression)Visit(node);
        }

        private BoundExpression VisitMetaclassArgument(BoundExpression node, Symbol metaclassMember)
        {
            BoundExpression metaclassArgument;
            if (_metaclassArguments.TryGetValue(metaclassMember, out metaclassArgument))
            {
                return metaclassArgument;
            }
            else
            {
                // No value was assigned to the field/property manually or in the metaclass constructor, so it contains the default value for the type
                return new BoundDefaultOperator(node.Syntax, node.Type) { WasCompilerGenerated = true };
            }
        }

        private BoundExpression MakeSimpleStaticValueExpression(CompileTimeValue value, TypeSymbol type, CSharpSyntaxNode syntax)
        {
            Debug.Assert(value.Kind == CompileTimeValueKind.Simple);
            if (value is ConstantStaticValue)
            {
                ConstantValue constantValue = ((ConstantStaticValue)value).Value;
                if (constantValue.IsNull)
                {
                    Debug.Assert(type.IsReferenceType || type.IsNullableTypeOrTypeParameter());
                    return new BoundLiteral(syntax, constantValue, type) { WasCompilerGenerated = true };
                }
                else
                {
                    return MetaUtils.ConvertIfNeeded(
                        type,
                        new BoundLiteral(syntax, constantValue, _compilation.GetSpecialType(constantValue.SpecialType)) { WasCompilerGenerated = true },
                        _compilation);
                }
            }
            else
            {
                Debug.Assert(value is TypeValue);
                return MetaUtils.ConvertIfNeeded(
                    type,
                    new BoundTypeOfOperator(
                        syntax,
                        new BoundTypeExpression(syntax, null, ((TypeValue)value).Type) { WasCompilerGenerated = true },
                        null,
                        _compilation.GetWellKnownType(WellKnownType.System_Type))
                    {
                        WasCompilerGenerated = true,
                    },
                    _compilation);
            }
        }

        private BoundBadExpression MakeBadExpression(CSharpSyntaxNode syntax, TypeSymbol type)
        {
            return new BoundBadExpression(syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, ImmutableArray<BoundNode>.Empty, type, true)
            {
                WasCompilerGenerated = true,
            };
        }
    }
}
