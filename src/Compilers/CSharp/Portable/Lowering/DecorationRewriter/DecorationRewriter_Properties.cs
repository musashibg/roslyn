using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    partial class DecorationRewriter
    {
        public override DecorationRewriteResult VisitPropertyAccess(BoundPropertyAccess node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            BoundExpression receiverOpt = node.ReceiverOpt;
            DecorationRewriteResult receiverResult = Visit(receiverOpt, variableValues);
            if (receiverResult != null)
            {
                variableValues = receiverResult.UpdatedVariableValues;
            }

            BoundExpression rewrittenNode = null;
            CompileTimeValue value = null;
            PropertySymbol property = node.PropertySymbol;

            // Handle well-known property accesses with static binding time
            if (receiverOpt != null)
            {
                Debug.Assert(receiverResult != null);
                CompileTimeValue receiverValue = receiverResult.Value;
                if (receiverValue.Kind != CompileTimeValueKind.Dynamic)
                {
                    if (receiverOpt.Type.IsArray())
                    {
                        if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Array__Length))
                        {
                            Debug.Assert(receiverValue.Kind != CompileTimeValueKind.Simple);
                            switch (receiverValue.Kind)
                            {
                                case CompileTimeValueKind.Complex:
                                    Debug.Assert(receiverValue is ArrayValue);
                                    value = new ConstantStaticValue(ConstantValue.Create(((ArrayValue)receiverValue).Array.Length));
                                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                                    break;

                                case CompileTimeValueKind.ArgumentArray:
                                    value = new ConstantStaticValue(ConstantValue.Create(((ArgumentArrayValue)receiverValue).ArgumentSymbols.Length));
                                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                                    break;
                            }
                        }
                    }
                    if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Type))
                    {
                        VisitStaticTypePropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                    else if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_MethodInfo))
                    {
                        VisitStaticMethodInfoPropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                    else if (receiverOpt.Type == _compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo))
                    {
                        VisitStaticParameterInfoPropertyAccess(node, property, receiverValue, out rewrittenNode, out value);
                    }
                }
            }

            if (rewrittenNode == null)
            {
                rewrittenNode = node.Update((BoundExpression)receiverResult?.Node, node.PropertySymbol, node.ResultKind, node.Type);
                if (value == null)
                {
                    value = CompileTimeValue.Dynamic;
                }
            }

            // A lone property access should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(rewrittenNode, variableValues, false, value);
        }

        public override DecorationRewriteResult VisitPropertyGroup(BoundPropertyGroup node, ImmutableDictionary<Symbol, CompileTimeValue> variableValues)
        {
            DecorationRewriteResult receiverResult = Visit(node.ReceiverOpt, variableValues);
            if (receiverResult != null)
            {
                variableValues = receiverResult.UpdatedVariableValues;
            }

            // A lone property group expression should never be a stand-alone statement, so we return MustEmit = false
            return new DecorationRewriteResult(
                node.Update(node.Properties, (BoundExpression)receiverResult?.Node, node.ResultKind),
                variableValues,
                false,
                CompileTimeValue.Dynamic);
        }

        private void VisitStaticTypePropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol property,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Name)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
                }
            }
            else
            {
                Debug.Assert(receiverValue is TypeValue);
                TypeSymbol type = ((TypeValue)receiverValue).Type;
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__FullName))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.GetTypeFullName(type)));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAbstract))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsAbstract));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsArray))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsArray()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsByRef))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(((TypeValue)receiverValue).IsByRef));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsClass))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsClassType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsConstructedGenericType))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type) && !type.IsUnboundGenericType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsEnum))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsEnumType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericParameter))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsTypeParameter()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericType))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsGeneric(type)));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsGenericTypeDefinition))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsUnboundGenericType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsInterface))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsInterfaceType()));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNested))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedAssembly))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Internal));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPrivate))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Private));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNestedPublic))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType != null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsNotPublic))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility != Accessibility.Public));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsPublic))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.ContainingType == null && type.DeclaredAccessibility == Accessibility.Public));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsSealed))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsSealed));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsValueType))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.IsValueType));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsVisible))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.CheckTypeIsVisible(type)));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Name))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(type.MetadataName));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__Namespace))
                {
                    NamespaceSymbol @namespace = type.ContainingNamespace;
                    if (@namespace == null)
                    {
                        value = new ConstantStaticValue(ConstantValue.Null);
                    }
                    else
                    {
                        value = new ConstantStaticValue(ConstantValue.Create(MetaUtils.GetNamespaceFullName(@namespace)));
                    }
                }
                else
                {
                    value = null;
                }

                if (value == null)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }

        private void VisitStaticMethodInfoPropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol property,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
                }
            }
            else
            {
                Debug.Assert(receiverValue is MethodInfoValue);
                MethodSymbol method = ((MethodInfoValue)receiverValue).Method;
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__DeclaringType))
                {
                    TypeSymbol containingType = method.ContainingType;
                    if (containingType == null)
                    {
                        value = new ConstantStaticValue(ConstantValue.Null);
                    }
                    else
                    {
                        value = new TypeValue(containingType);
                    }
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MemberInfo__Name))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(method.Name));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodInfo__ReturnType))
                {
                    value = new TypeValue(method.ReturnType);
                }
                else
                {
                    value = null;
                }

                if (value == null)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }

        private void VisitStaticParameterInfoPropertyAccess(
            BoundPropertyAccess node,
            PropertySymbol property,
            CompileTimeValue receiverValue,
            out BoundExpression rewrittenNode,
            out CompileTimeValue value)
        {
            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType)
                    || property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position))
                {
                    _diagnostics.Add(ErrorCode.ERR_StaticNullReference, node.Syntax.Location);
                    rewrittenNode = MakeBadExpression(node.Syntax, node.Type);
                    value = CompileTimeValue.Dynamic;
                }
                else
                {
                    rewrittenNode = null;
                    value = null;
                }
            }
            else
            {
                Debug.Assert(receiverValue is ParameterInfoValue);
                ParameterSymbol parameter = ((ParameterInfoValue)receiverValue).Parameter;
                if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__IsOut))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(parameter.RefKind == RefKind.Out));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Member))
                {
                    // For now, we only deal at compile time with parameters of methods
                    Debug.Assert(parameter.ContainingSymbol is MethodSymbol);
                    value = new MethodInfoValue((MethodSymbol)parameter.ContainingSymbol);
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Name))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(parameter.Name));
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__ParameterType))
                {
                    value = new TypeValue(parameter.Type, parameter.RefKind != RefKind.None);
                }
                else if (property == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_ParameterInfo__Position))
                {
                    value = new ConstantStaticValue(ConstantValue.Create(parameter.Ordinal));
                }
                else
                {
                    value = null;
                }

                if (value == null || value.Kind != CompileTimeValueKind.Simple)
                {
                    rewrittenNode = null;
                }
                else
                {
                    rewrittenNode = MakeSimpleStaticValueExpression(value, node.Type, node.Syntax);
                }
            }
        }
    }
}
