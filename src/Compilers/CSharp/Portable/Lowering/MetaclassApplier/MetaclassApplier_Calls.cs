using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Meta
{
    partial class MetaclassApplier
    {
        public override CompileTimeValue VisitCall(BoundCall node, object arg)
        {
            MethodSymbol method = node.Method;

            CompileTimeValue receiverValue = Visit(node.ReceiverOpt, arg);
            ImmutableArray<CompileTimeValue> argumentValues = VisitList(node.Arguments, arg);

            // Handle well-known method invocations with static binding time
            if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetConstructors)
                || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetConstructors2))
            {
                return VisitGetConstructors(node, receiverValue, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetMethods)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetMethods2))
            {
                return VisitGetMethods(node, receiverValue, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetProperties)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetProperties2))
            {
                return VisitGetProperties(node, receiverValue, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Type__IsAssignableFrom)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaExtensions__IsAssignableFrom))
            {
                return VisitIsAssignableFromCall(node, receiverValue, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_MethodBase__GetParameters))
            {
                return VisitGetParametersCall(node, receiverValue, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_PropertyInfo__GetAccessors))
            {
                return VisitGetAccessorsCall(node, receiverValue, argumentValues);
            }
            else if (method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T)
                     || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2))
            {
                return VisitGetCustomAttributesCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__AddTrait)
                     || method.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__AddTrait_T))
            {
                return VisitAddTraitCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ApplyDecorator))
            {
                return VisitApplyDecoratorCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__IsImplicitlyDeclared))
            {
                return VisitIsImplicitlyDeclaredCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__IsIterator))
            {
                return VisitIsIteratorCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__IsPropertyAccessor))
            {
                return VisitIsPropertyAccessorCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__IsReadOnly))
            {
                return VisitIsReadOnlyCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__IsWriteOnly))
            {
                return VisitIsWriteOnlyCall(node, argumentValues);
            }
            else if (method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType)
                     || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ParameterType2))
            {
                return VisitParameterTypeCall(node, argumentValues);
            }
            else
            {
                Debug.Assert(method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType)
                             || method == _compilation.GetWellKnownTypeMember(WellKnownMember.CSharp_Meta_MetaPrimitives__ThisObjectType2));
                return VisitThisObjectTypeCall(node, argumentValues);
            }
        }

        private CompileTimeValue VisitGetConstructors(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(receiverValue != null && argumentValues.Length <= 1);

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(receiverValue is TypeValue && node.Type.IsArray());

            BindingFlags bindingFlags;
            if (argumentValues.IsEmpty)
            {
                bindingFlags = BindingFlags.Instance | BindingFlags.Public;
            }
            else
            {
                var bindingFlagsValue = argumentValues[0] as EnumValue;
                Debug.Assert(bindingFlagsValue != null && bindingFlagsValue.EnumType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags));
                bindingFlags = (BindingFlags)bindingFlagsValue.UnderlyingValue.Int32Value;
            }

            return StaticValueUtils.LookupConstructors(((TypeValue)receiverValue).Type, bindingFlags, (ArrayTypeSymbol)node.Type);
        }

        private CompileTimeValue VisitGetMethods(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(receiverValue != null && argumentValues.Length <= 1);

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(receiverValue is TypeValue && node.Type.IsArray());

            BindingFlags bindingFlags;
            if (argumentValues.IsEmpty)
            {
                bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
            }
            else
            {
                var bindingFlagsValue = argumentValues[0] as EnumValue;
                Debug.Assert(bindingFlagsValue != null && bindingFlagsValue.EnumType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags));
                bindingFlags = (BindingFlags)bindingFlagsValue.UnderlyingValue.Int32Value;
            }

            return StaticValueUtils.LookupMethods(((TypeValue)receiverValue).Type, bindingFlags, (ArrayTypeSymbol)node.Type);
        }

        private CompileTimeValue VisitGetProperties(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(receiverValue != null && argumentValues.Length <= 1);

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(receiverValue is TypeValue && node.Type.IsArray());

            BindingFlags bindingFlags;
            if (argumentValues.IsEmpty)
            {
                bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
            }
            else
            {
                var bindingFlagsValue = argumentValues[0] as EnumValue;
                Debug.Assert(bindingFlagsValue != null && bindingFlagsValue.EnumType == _compilation.GetWellKnownType(WellKnownType.System_Reflection_BindingFlags));
                bindingFlags = (BindingFlags)bindingFlagsValue.UnderlyingValue.Int32Value;
            }

            return StaticValueUtils.LookupProperties(((TypeValue)receiverValue).Type, bindingFlags, (ArrayTypeSymbol)node.Type);
        }

        private CompileTimeValue VisitIsAssignableFromCall(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");

            CompileTimeValue targetTypeValue;
            CompileTimeValue sourceTypeValue;
            bool isExtensionMethod;
            MethodSymbol method = node.Method;
            CSharpSyntaxNode syntax = node.Syntax;
            if (method.IsStatic)
            {
                Debug.Assert(argumentValues.Length == 2);
                targetTypeValue = argumentValues[0];
                sourceTypeValue = argumentValues[1];
                isExtensionMethod = true;
            }
            else
            {
                Debug.Assert(receiverValue != null && argumentValues.Length == 1);
                targetTypeValue = receiverValue;
                sourceTypeValue = argumentValues[0];
                isExtensionMethod = false;
            }

            if (targetTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)targetTypeValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, (isExtensionMethod ? node.Arguments[0] : node.ReceiverOpt).Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            if (sourceTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)sourceTypeValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, (isExtensionMethod ? node.Arguments[1] : node.Arguments[0]).Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(targetTypeValue is TypeValue && sourceTypeValue is TypeValue);
            return new ConstantStaticValue(
                ConstantValue.Create(
                    MetaUtils.CheckTypeIsAssignableFrom(((TypeValue)receiverValue).Type, ((TypeValue)sourceTypeValue).Type)));
        }

        private CompileTimeValue VisitGetParametersCall(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(receiverValue != null && argumentValues.IsEmpty);

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            MethodSymbol method;
            if (receiverValue is MethodInfoValue)
            {
                method = ((MethodInfoValue)receiverValue).Method;
            }
            else
            {
                Debug.Assert(receiverValue is ConstructorInfoValue);
                method = ((ConstructorInfoValue)receiverValue).Constructor;
            }
            return new ArrayValue(
                _compilation.CreateArrayTypeSymbol(_compilation.GetWellKnownType(WellKnownType.System_Reflection_ParameterInfo)),
                method.Parameters.SelectAsArray(p => (CompileTimeValue)(new ParameterInfoValue(p))));
        }

        private CompileTimeValue VisitGetAccessorsCall(BoundCall node, CompileTimeValue receiverValue, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(receiverValue != null && argumentValues.IsEmpty);

            if (receiverValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)receiverValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.ReceiverOpt.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(receiverValue is PropertyInfoValue);
            PropertySymbol property = ((PropertyInfoValue)receiverValue).Property;

            ImmutableArray<CompileTimeValue>.Builder accessorsBuilder = ImmutableArray.CreateBuilder<CompileTimeValue>();
            if (property.GetMethod != null)
            {
                accessorsBuilder.Add(new MethodInfoValue(property.GetMethod));
            }
            if (property.SetMethod != null)
            {
                accessorsBuilder.Add(new MethodInfoValue(property.SetMethod));
            }
            return new ArrayValue(
                _compilation.CreateArrayTypeSymbol(_compilation.GetWellKnownType(WellKnownType.System_Reflection_PropertyInfo)),
                accessorsBuilder.ToImmutable());
        }

        private CompileTimeValue VisitGetCustomAttributesCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");

            Debug.Assert(argumentValues.Length == 1 && node.Method.TypeArguments.Length == 1);
            CompileTimeValue argumentValue = argumentValues[0];

            if (argumentValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)argumentValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            MethodSymbol invokedMethod = node.Method;
            TypeSymbol requestedAttributeType = invokedMethod.TypeArguments[0];
            CSharpAttributeData candidateAttribute;
            if (argumentValue.Kind == CompileTimeValueKind.Simple)
            {
                Debug.Assert(argumentValue is TypeValue
                             && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                TypeSymbol type = ((TypeValue)argumentValue).Type;
                return StaticValueUtils.LookupCustomAttributeValue(
                    node.Syntax,
                    requestedAttributeType,
                    type.GetAttributes(),
                    _diagnostics,
                    ImmutableArray.Create(_applicationLocation),
                    out candidateAttribute);
            }
            else
            {
                Debug.Assert(argumentValue.Kind == CompileTimeValueKind.Complex);
                if (argumentValue is MethodInfoValue)
                {
                    Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                    MethodSymbol method = ((MethodInfoValue)argumentValue).Method;
                    return StaticValueUtils.LookupCustomAttributeValue(
                        node.Syntax,
                        requestedAttributeType,
                        method.GetAttributes(),
                        _diagnostics,
                        ImmutableArray.Create(_applicationLocation),
                        out candidateAttribute);
                }
                else if (argumentValue is ConstructorInfoValue)
                {
                    Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                    MethodSymbol constructor = ((ConstructorInfoValue)argumentValue).Constructor;
                    return StaticValueUtils.LookupCustomAttributeValue(
                        node.Syntax,
                        requestedAttributeType,
                        constructor.GetAttributes(),
                        _diagnostics,
                        ImmutableArray.Create(_applicationLocation),
                        out candidateAttribute);
                }
                else if (argumentValue is PropertyInfoValue)
                {
                    Debug.Assert(invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T));
                    PropertySymbol property = ((PropertyInfoValue)argumentValue).Property;
                    return StaticValueUtils.LookupCustomAttributeValue(
                        node.Syntax,
                        requestedAttributeType,
                        property.GetAttributes(),
                        _diagnostics,
                        ImmutableArray.Create(_applicationLocation),
                        out candidateAttribute);
                }
                else
                {
                    Debug.Assert(argumentValue is ParameterInfoValue
                                 && invokedMethod.OriginalDefinition == _compilation.GetWellKnownTypeMember(WellKnownMember.System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2));
                    ParameterSymbol parameter = ((ParameterInfoValue)argumentValue).Parameter;
                    return StaticValueUtils.LookupCustomAttributeValue(
                        node.Syntax,
                        requestedAttributeType,
                        parameter.GetAttributes(),
                        _diagnostics,
                        ImmutableArray.Create(_applicationLocation),
                        out candidateAttribute);
                }
            }
        }

        private CompileTimeValue VisitAddTraitCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length >= 1);

            CompileTimeValue hostTypeValue = argumentValues[0];

            if (hostTypeValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)hostTypeValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(hostTypeValue is TypeValue);
            TypeSymbol hostType = ((TypeValue)hostTypeValue).Type;

            if (hostType != _targetType)
            {
                AddDiagnostic(ErrorCode.ERR_MetaclassModificationOnNonTargetType, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            MethodSymbol method = node.Method;
            TypeSymbol traitType;
            if (method.Arity == 0)
            {
                Debug.Assert(argumentValues.Length == 2);
                CompileTimeValue traitTypeValue = argumentValues[1];

                if (traitTypeValue is ConstantStaticValue)
                {
                    Debug.Assert(((ConstantStaticValue)traitTypeValue).Value.IsNull);
                    AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[1].Syntax.Location);
                    throw new ExecutionInterruptionException(InterruptionKind.Throw);
                }

                Debug.Assert(traitTypeValue is TypeValue);
                traitType = ((TypeValue)hostTypeValue).Type;
            }
            else
            {
                Debug.Assert(method.Arity == 1 && argumentValues.Length == 1 && !method.TypeArguments.IsDefaultOrEmpty);
                traitType = method.TypeArguments[0];
            }

            if (traitType.BaseType != _compilation.GetWellKnownType(WellKnownType.CSharp_Meta_Trait))
            {
                AddDiagnostic(ErrorCode.ERR_NotATrait, node.Syntax.Location, traitType.Name);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            var hostSourceType = hostType as SourceMemberContainerTypeSymbol;
            Debug.Assert(hostSourceType != null);

            var traitSourceType = traitType as SourceMemberContainerTypeSymbol;
            if (traitSourceType == null)
            {
                AddDiagnostic(ErrorCode.ERR_NonSourceTrait, node.Syntax.Location, traitType.Name);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
            else if (traitSourceType.IsGenericType)
            {
                AddDiagnostic(ErrorCode.ERR_GenericTraitTypesNotSupported, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            hostSourceType.AddTrait(traitSourceType, _diagnostics, node.Syntax.Location, _cancellationToken);
            return new ConstantStaticValue(ConstantValue.Null);
        }

        private CompileTimeValue VisitApplyDecoratorCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 2);

            CompileTimeValue memberInfoValue = argumentValues[0];
            var decoratorValue = argumentValues[1] as DecoratorValue;

            Debug.Assert(decoratorValue != null);

            Symbol member;
            if (memberInfoValue is MethodInfoValue)
            {
                member = ((MethodInfoValue)memberInfoValue).Method;
            }
            else if (memberInfoValue is ConstructorInfoValue)
            {
                member = ((ConstructorInfoValue)memberInfoValue).Constructor;
            }
            else
            {
                Debug.Assert(memberInfoValue is PropertyInfoValue);
                member = ((PropertyInfoValue)memberInfoValue).Property;
            }

            if (member.ContainingType != _targetType)
            {
                AddDiagnostic(ErrorCode.ERR_MetaclassModificationOnNonTargetType, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
            else if (member is SourcePropertyAccessorSymbol)
            {
                AddDiagnostic(ErrorCode.ERR_DecoratedPropertyAccessor, node.Syntax.Location, member);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            var decoratorData = decoratorValue.CreateDecoratorData(new SimpleSyntaxReference(node.Syntax));
            if (member is SourceMethodSymbol)
            {
                ((SourceMethodSymbol)member).ApplyDecorator(decoratorData);
            }
            else if (member is SourcePropertySymbol)
            {
                ((SourcePropertySymbol)member).ApplyDecorator(decoratorData);
            }
            else
            {
                Debug.Assert(member.IsImplicitlyDeclared);
                AddDiagnostic(ErrorCode.ERR_DecoratedImplicitlyDeclaredMember, node.Syntax.Location, member);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
            return new ConstantStaticValue(ConstantValue.Null);
        }

        private CompileTimeValue VisitIsImplicitlyDeclaredCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 1);

            CompileTimeValue memberInfoValue = argumentValues[0];

            if (memberInfoValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)memberInfoValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Symbol member;
            if (memberInfoValue is TypeValue)
            {
                member = ((TypeValue)memberInfoValue).Type;
            }
            else if (memberInfoValue is MethodInfoValue)
            {
                member = ((MethodInfoValue)memberInfoValue).Method;
            }
            else if (memberInfoValue is ConstructorInfoValue)
            {
                member = ((ConstructorInfoValue)memberInfoValue).Constructor;
            }
            else
            {
                Debug.Assert(memberInfoValue is PropertyInfoValue);
                member = ((PropertyInfoValue)memberInfoValue).Property;
            }

            return new ConstantStaticValue(ConstantValue.Create(member.IsImplicitlyDeclared));
        }

        private CompileTimeValue VisitIsIteratorCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 1);

            CompileTimeValue methodInfoValue = argumentValues[0];

            if (methodInfoValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)methodInfoValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(methodInfoValue is MethodInfoValue);
            MethodSymbol method = ((MethodInfoValue)methodInfoValue).Method;

            return new ConstantStaticValue(ConstantValue.Create(method.IsIterator));
        }

        private CompileTimeValue VisitIsPropertyAccessorCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 1);

            CompileTimeValue methodInfoValue = argumentValues[0];

            if (methodInfoValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)methodInfoValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(methodInfoValue is MethodInfoValue);
            MethodSymbol method = ((MethodInfoValue)methodInfoValue).Method;

            bool isPropertyAccessor = (method.MethodKind == MethodKind.PropertyGet || method.MethodKind == MethodKind.PropertySet);
            return new ConstantStaticValue(ConstantValue.Create(isPropertyAccessor));
        }

        private CompileTimeValue VisitIsReadOnlyCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 1);

            CompileTimeValue propertyInfoValue = argumentValues[0];

            if (propertyInfoValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)propertyInfoValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(propertyInfoValue is PropertyInfoValue);
            PropertySymbol property = ((PropertyInfoValue)propertyInfoValue).Property;

            bool isReadOnly = (property.SetMethod == null);
            return new ConstantStaticValue(ConstantValue.Create(isReadOnly));
        }

        private CompileTimeValue VisitIsWriteOnlyCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 1);

            CompileTimeValue propertyInfoValue = argumentValues[0];

            if (propertyInfoValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)propertyInfoValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Debug.Assert(propertyInfoValue is PropertyInfoValue);
            PropertySymbol property = ((PropertyInfoValue)propertyInfoValue).Property;

            bool isWriteOnly = (property.GetMethod == null);
            return new ConstantStaticValue(ConstantValue.Create(isWriteOnly));
        }

        private CompileTimeValue VisitParameterTypeCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 2);

            CompileTimeValue memberInfoValue = argumentValues[0];
            CompileTimeValue parameterIndexValue = argumentValues[1];

            if (memberInfoValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)memberInfoValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Symbol member;
            if (memberInfoValue is MethodInfoValue)
            {
                member = ((MethodInfoValue)memberInfoValue).Method;
            }
            else if (memberInfoValue is ConstructorInfoValue)
            {
                member = ((ConstructorInfoValue)memberInfoValue).Constructor;
            }
            else
            {
                Debug.Assert(memberInfoValue is PropertyInfoValue);
                member = ((PropertyInfoValue)memberInfoValue).Property;
            }

            Debug.Assert(parameterIndexValue is ConstantStaticValue);
            ConstantValue parameterIndexConstantValue = ((ConstantStaticValue)parameterIndexValue).Value;
            Debug.Assert(parameterIndexConstantValue.SpecialType == SpecialType.System_Int32);
            int parameterIndex = parameterIndexConstantValue.Int32Value;

            int parameterCount;
            if (member.Kind == SymbolKind.Method)
            {
                parameterCount = ((MethodSymbol)member).ParameterCount;
            }
            else
            {
                Debug.Assert(member.Kind == SymbolKind.Property);
                parameterCount = ((PropertySymbol)member).ParameterCount;
            }

            if (parameterIndex < 0 || parameterIndex >= parameterCount)
            {
                AddDiagnostic(ErrorCode.ERR_StaticIndexOutOfBounds, node.Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }
            else
            {
                TypeSymbol parameterType;
                if (member.Kind == SymbolKind.Method)
                {
                    parameterType = ((MethodSymbol)member).ParameterTypes[parameterIndex];
                }
                else
                {
                    Debug.Assert(member.Kind == SymbolKind.Property);
                    parameterType = ((PropertySymbol)member).ParameterTypes[parameterIndex];
                }

                return new TypeValue(parameterType);
            }
        }

        private CompileTimeValue VisitThisObjectTypeCall(BoundCall node, ImmutableArray<CompileTimeValue> argumentValues)
        {
            Debug.Assert(node.ArgsToParamsOpt.IsDefault, "Reordered arguments are not supported by MetaclassApplier.");
            Debug.Assert(argumentValues.Length == 1);

            CompileTimeValue memberInfoValue = argumentValues[0];

            if (memberInfoValue is ConstantStaticValue)
            {
                Debug.Assert(((ConstantStaticValue)memberInfoValue).Value.IsNull);
                AddDiagnostic(ErrorCode.ERR_StaticNullReference, node.Arguments[0].Syntax.Location);
                throw new ExecutionInterruptionException(InterruptionKind.Throw);
            }

            Symbol member;
            if (memberInfoValue is MethodInfoValue)
            {
                member = ((MethodInfoValue)memberInfoValue).Method;
            }
            else if (memberInfoValue is ConstructorInfoValue)
            {
                member = ((ConstructorInfoValue)memberInfoValue).Constructor;
            }
            else
            {
                Debug.Assert(memberInfoValue is PropertyInfoValue);
                member = ((PropertyInfoValue)memberInfoValue).Property;
            }

            TypeSymbol thisObjectType = member.IsStatic
                                            ? _compilation.GetSpecialType(SpecialType.System_Void)
                                            : member.ContainingType;
            return new TypeValue(thisObjectType);
        }
    }
}
