// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.RuntimeMembers;
using System;
using System.IO;

namespace Microsoft.CodeAnalysis
{
    internal static class WellKnownMembers
    {
        private readonly static ImmutableArray<MemberDescriptor> s_descriptors;

        static WellKnownMembers()
        {
            ushort[] initializationValues = new ushort[]
            {
                // System_Math__RoundDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Math,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Math__PowDoubleDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Math,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Array__get_Length
                (ushort)MemberFlags.PropertyGet,                                                                            // Flags
                (ushort)WellKnownType.System_Array,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Array__Empty
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Array,                                                                         // DeclaringTypeId
                1,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray,
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Array__Length
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Array,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Convert__ToBooleanDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToBooleanInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Convert__ToBooleanUInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,

                // System_Convert__ToBooleanInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // System_Convert__ToBooleanUInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,

                // System_Convert__ToBooleanSingle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToBooleanDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToSByteDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_SByte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToSByteDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_SByte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToSByteSingle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_SByte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToByteDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToByteDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToByteSingle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToInt16Decimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToInt16Double
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToInt16Single
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToUInt16Decimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToUInt16Double
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToUInt16Single
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToInt32Decimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToInt32Double
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToInt32Single
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToUInt32Decimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToUInt32Double
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToUInt32Single
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToInt64Decimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToInt64Double
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToInt64Single
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToUInt64Decimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToUInt64Double
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Convert__ToUInt64Single
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Convert__ToSingleDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Convert__ToDoubleDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Convert,                                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_CLSCompliantAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_CLSCompliantAttribute,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_FlagsAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_FlagsAttribute,                                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Guid__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Guid,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Type__FullName
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Type__GetConstructors
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_ConstructorInfo,

                // System_Type__GetConstructor2
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_ConstructorInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_BindingFlags,

                // System_Type__GetMethods
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodInfo,

                // System_Type__GetMethods2
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_BindingFlags,

                // System_Type__GetProperties
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_PropertyInfo,

                // System_Type__GetProperties2
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_PropertyInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_BindingFlags,

                // System_Type__GetProperty
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_PropertyInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_BindingFlags,

                // System_Type__GetTypeFromCLSID
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Guid,

                // System_Type__GetTypeFromHandle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeTypeHandle,

                // System_Type__IsAbstract
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsArray
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsAssignableFrom
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Type__IsByRef
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsClass
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsConstructedGenericType
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsEnum
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsInterface
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsGenericParameter
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsGenericType
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsGenericTypeDefinition
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsNested
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsNestedAssembly
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsNestedPrivate
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsNestedPublic
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsNotPublic
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsPublic
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsSealed
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsValueType
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__IsVisible
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Type__Missing
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,                                // Field Signature

                // System_Type__Namespace
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Type,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Reflection_AssemblyKeyFileAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Reflection_AssemblyKeyFileAttribute,                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Reflection_AssemblyKeyNameAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Reflection_AssemblyKeyNameAttribute,                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Reflection_MemberInfo__DeclaringType
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_MemberInfo,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Reflection_MemberInfo__Name
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_MemberInfo,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Reflection_MethodBase__GetMethodFromHandle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Reflection_MethodBase,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodBase,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeMethodHandle,

                // System_Reflection_MethodBase__GetMethodFromHandle2
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Reflection_MethodBase,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodBase,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeMethodHandle,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeTypeHandle,

                // System_Reflection_MethodBase__GetParameters
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Reflection_MethodBase,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_ParameterInfo,

                // System_Reflection_MethodBase__Invoke
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Reflection_MethodBase,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Reflection_MethodBase__IsStatic
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Reflection_MethodBase,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Reflection_MethodInfo__CreateDelegate
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Reflection_MethodInfo,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Reflection_MethodInfo__ReturnType
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_MethodInfo,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Reflection_ParameterInfo__IsOut
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Reflection_ParameterInfo,                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Reflection_ParameterInfo__Member
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_ParameterInfo,                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MemberInfo,

                // System_Reflection_ParameterInfo__Name
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_ParameterInfo,                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Reflection_ParameterInfo__ParameterType
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_ParameterInfo,                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Reflection_ParameterInfo__Position
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_ParameterInfo,                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Reflection_PropertyInfo__GetAccessors
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Reflection_PropertyInfo,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodInfo,

                // System_Reflection_PropertyInfo__GetValue
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Reflection_PropertyInfo,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Reflection_PropertyInfo__GetValue2
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Reflection_PropertyInfo,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Reflection_PropertyInfo__PropertyType
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)WellKnownType.System_Reflection_PropertyInfo,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Reflection_PropertyInfo__SetValue
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Reflection_PropertyInfo,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Reflection_PropertyInfo__SetValue2
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Reflection_PropertyInfo,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Delegate__CreateDelegate
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Delegate,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodInfo,

                // System_Delegate__CreateDelegate4
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Delegate,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Reflection_FieldInfo__GetFieldFromHandle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Reflection_FieldInfo,                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_FieldInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeFieldHandle,

                // System_Reflection_FieldInfo__GetFieldFromHandle2
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Reflection_FieldInfo,                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_FieldInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeFieldHandle,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeTypeHandle,

                // System_Reflection_Missing__Value
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Reflection_Missing,                                                            // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_Missing,                      // Field Signature

                // System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Reflection_CustomAttributeExtensions,                                          // DeclaringTypeId
                1,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MemberInfo,

                // System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Reflection_CustomAttributeExtensions,                                          // DeclaringTypeId
                1,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_ParameterInfo,

                // System_IEquatable_T__Equals
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_IEquatable_T,                                                                  // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Collections_Generic_EqualityComparer_T__Equals
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Collections_Generic_EqualityComparer_T,                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Collections_Generic_EqualityComparer_T__GetHashCode
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Collections_Generic_EqualityComparer_T,                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Collections_Generic_EqualityComparer_T__get_Default
                (ushort)(MemberFlags.PropertyGet | MemberFlags.Static),                                                     // Flags
                (ushort)WellKnownType.System_Collections_Generic_EqualityComparer_T,                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Collections_Generic_EqualityComparer_T,

                // System_AttributeUsageAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_AttributeUsageAttribute,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, 0,

                // System_AttributeUsageAttribute__AllowMultiple
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_AttributeUsageAttribute,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_AttributeUsageAttribute__Inherited
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_AttributeUsageAttribute,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_ParamArrayAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_ParamArrayAttribute,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_STAThreadAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_STAThreadAttribute,                                                            // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Reflection_DefaultMemberAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Reflection_DefaultMemberAttribute,                                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Diagnostics_Debugger__Break
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Diagnostics_Debugger,                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Diagnostics_DebuggerDisplayAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggerDisplayAttribute,                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Diagnostics_DebuggerDisplayAttribute__Type
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggerDisplayAttribute,                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Diagnostics_DebuggerNonUserCodeAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggerNonUserCodeAttribute,                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Diagnostics_DebuggerHiddenAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggerHiddenAttribute,                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Diagnostics_DebuggerBrowsableAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggerBrowsableAttribute,                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Diagnostics_DebuggerBrowsableState,

                // System_Diagnostics_DebuggerStepThroughAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggerStepThroughAttribute,                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Diagnostics_DebuggableAttribute__ctorDebuggingModes
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute,                                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes,

                // System_Diagnostics_DebuggableAttribute_DebuggingModes__Default
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes, // Field Signature

                // System_Diagnostics_DebuggableAttribute_DebuggingModes__DisableOptimizations
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes, // Field Signature

                // System_Diagnostics_DebuggableAttribute_DebuggingModes__EnableEditAndContinue
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes, // Field Signature

                // System_Diagnostics_DebuggableAttribute_DebuggingModes__IgnoreSymbolStoreSequencePoints
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Diagnostics_DebuggableAttribute__DebuggingModes, // Field Signature

                // System_Runtime_InteropServices_UnknownWrapper__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_UnknownWrapper,                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Runtime_InteropServices_DispatchWrapper__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_DispatchWrapper,                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Runtime_InteropServices_ClassInterfaceAttribute__ctorClassInterfaceType
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_ClassInterfaceAttribute,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_ClassInterfaceType,

                // System_Runtime_InteropServices_CoClassAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_CoClassAttribute,                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Runtime_InteropServices_ComAwareEventInfo__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_ComAwareEventInfo,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Runtime_InteropServices_ComAwareEventInfo__AddEventHandler
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_ComAwareEventInfo,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,

                // System_Runtime_InteropServices_ComAwareEventInfo__RemoveEventHandler
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_ComAwareEventInfo,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,

                // System_Runtime_InteropServices_ComEventInterfaceAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_ComEventInterfaceAttribute,                            // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Runtime_InteropServices_ComSourceInterfacesAttribute__ctorString
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_ComSourceInterfacesAttribute,                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Runtime_InteropServices_ComVisibleAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_ComVisibleAttribute,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Runtime_InteropServices_DispIdAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_DispIdAttribute,                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Runtime_InteropServices_GuidAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_GuidAttribute,                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Runtime_InteropServices_InterfaceTypeAttribute__ctorComInterfaceType
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_InterfaceTypeAttribute,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_ComInterfaceType,

                // System_Runtime_InteropServices_InterfaceTypeAttribute__ctorInt16
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_InterfaceTypeAttribute,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,

                // System_Runtime_InteropServices_Marshal__GetTypeFromCLSID
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_Marshal,                                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Guid,

                // System_Runtime_InteropServices_TypeIdentifierAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_TypeIdentifierAttribute,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_InteropServices_TypeIdentifierAttribute__ctorStringString
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_TypeIdentifierAttribute,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Runtime_InteropServices_BestFitMappingAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_BestFitMappingAttribute,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Runtime_InteropServices_DefaultParameterValueAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_DefaultParameterValueAttribute,                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Runtime_InteropServices_LCIDConversionAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_LCIDConversionAttribute,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Runtime_InteropServices_UnmanagedFunctionPointerAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_UnmanagedFunctionPointerAttribute,                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_CallingConvention,

                // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__AddEventHandler
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T,          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__GetOrCreateEventRegistrationTokenTable
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T,          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T,
                    1,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T,
                    1,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__InvocationList
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T,          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__RemoveEventHandler
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T,          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken,

                // System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal__AddEventHandler_T
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal,                  // DeclaringTypeId
                1,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Func_T2,
                    2,
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Action_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken,
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal__RemoveAllEventHandlers
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal,                  // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Action_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken,

                // System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal__RemoveEventHandler_T
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal,                  // DeclaringTypeId
                1,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Action_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken,
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Runtime_CompilerServices_DateTimeConstantAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_DateTimeConstantAttribute,                            // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // System_Runtime_CompilerServices_DecimalConstantAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_DecimalConstantAttribute,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    5,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,

                // System_Runtime_CompilerServices_DecimalConstantAttribute__ctorByteByteInt32Int32Int32
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_DecimalConstantAttribute,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    5,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Runtime_CompilerServices_ExtensionAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_ExtensionAttribute,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_CompilerGeneratedAttribute,                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_AccessedThroughPropertyAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AccessedThroughPropertyAttribute,                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Runtime_CompilerServices_CompilationRelaxationsAttribute__ctorInt32
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_CompilationRelaxationsAttribute,                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Runtime_CompilerServices_RuntimeCompatibilityAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_RuntimeCompatibilityAttribute,                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_RuntimeCompatibilityAttribute__WrapNonExceptionThrows
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_RuntimeCompatibilityAttribute,                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Runtime_CompilerServices_UnsafeValueTypeAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_UnsafeValueTypeAttribute,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_FixedBufferAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_FixedBufferAttribute,                                 // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Runtime_CompilerServices_DynamicAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_DynamicAttribute,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_DynamicAttribute__ctorTransformFlags
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_DynamicAttribute,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Runtime_CompilerServices_CallSite_T__Create
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_CallSite_T,                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSite_T,
                    1,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,

                // System_Runtime_CompilerServices_CallSite_T__Target
                (ushort)MemberFlags.Field,                                                                                  // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_CallSite_T,                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,                                                        // Field Signature

                // System_Runtime_CompilerServices_RuntimeHelpers__GetObjectValueObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_RuntimeHelpers,                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_RuntimeHelpers__InitializeArrayArrayRuntimeFieldHandle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_RuntimeHelpers,                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Array,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_RuntimeFieldHandle,

                // System_Runtime_CompilerServices_RuntimeHelpers__get_OffsetToStringData
                (ushort)(MemberFlags.PropertyGet | MemberFlags.Static),                                                     // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_RuntimeHelpers,                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Runtime_ExceptionServices_ExceptionDispatchInfo__Capture
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Runtime_ExceptionServices_ExceptionDispatchInfo,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_ExceptionServices_ExceptionDispatchInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Exception,

                // System_Runtime_ExceptionServices_ExceptionDispatchInfo__Throw
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_ExceptionServices_ExceptionDispatchInfo,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Security_UnverifiableCodeAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Security_UnverifiableCodeAttribute,                                            // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Security_Permissions_SecurityAction__RequestMinimum
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Security_Permissions_SecurityAction,                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Security_Permissions_SecurityAction,     // Field Signature

                // System_Security_Permissions_SecurityPermissionAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Security_Permissions_SecurityPermissionAttribute,                              // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Security_Permissions_SecurityAction,

                // System_Security_Permissions_SecurityPermissionAttribute__SkipVerification
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Security_Permissions_SecurityPermissionAttribute,                              // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Activator__CreateInstance
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Activator,                                                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Activator__CreateInstance_T
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Activator,                                                                     // DeclaringTypeId
                1,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Threading_Interlocked__CompareExchange_T
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Threading_Interlocked,                                                         // DeclaringTypeId
                1,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Threading_Monitor__Enter
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Threading_Monitor,                                                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Threading_Monitor__Enter2
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Threading_Monitor,                                                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Threading_Monitor__Exit
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Threading_Monitor,                                                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Threading_Thread__CurrentThread
                (ushort)(MemberFlags.Property | MemberFlags.Static),                                                        // Flags
                (ushort)WellKnownType.System_Threading_Thread,                                                              // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Threading_Thread,

                // System_Threading_Thread__ManagedThreadId
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Threading_Thread,                                                              // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // Microsoft_CSharp_RuntimeBinder_Binder__BinaryOperation
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Linq_Expressions_ExpressionType,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__Convert
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // Microsoft_CSharp_RuntimeBinder_Binder__GetIndex
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__GetMember
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__Invoke
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__InvokeConstructor
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__InvokeMember
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    5,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__IsEvent
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // Microsoft_CSharp_RuntimeBinder_Binder__SetIndex
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__SetMember
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_Binder__UnaryOperation
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_Binder,                                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_CallSiteBinder,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpBinderFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Linq_Expressions_ExpressionType,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,
                    1,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,

                // Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo__Create
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfoFlags,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDecimalBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToBooleanString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToSByteString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_SByte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToByteString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToShortString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToUShortString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToIntegerString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToUIntegerString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToLongString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToULongString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToSingleString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDoubleString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDecimalString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDateString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharArrayRankOneString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringByte
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringUInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringUInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringSingle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringDateTime
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringChar
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToBooleanObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToSByteObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_SByte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToByteObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToShortObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToUShortObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToIntegerObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToUIntegerObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToLongObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToULongObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToSingleObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDoubleObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDecimalObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDateObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharArrayRankOneObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ToGenericParameter_T_Object
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                1,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Conversions__ChangeType
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Conversions,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // Microsoft_VisualBasic_CompilerServices_Operators__PlusObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__NegateObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__NotObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__AndObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__OrObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__XorObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__AddObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__SubtractObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__MultiplyObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__DivideObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__ExponentObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__ModObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__IntDivideObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__LeftShiftObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__RightShiftObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__ConcatenateObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectNotEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectLessObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectLessEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectGreaterEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectGreaterObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectNotEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectLessObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectLessEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectGreaterEqualObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectGreaterObjectObjectBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_Operators__CompareStringStringStringBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Operators,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_EmbeddedOperators__CompareStringStringStringBoolean
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_EmbeddedOperators,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateCall
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_NewLateBinding,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    8,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateGet
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_NewLateBinding,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    7,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateSet
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_NewLateBinding,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    6,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateSetComplex
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_NewLateBinding,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    8,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateIndexGet
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_NewLateBinding,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateIndexSet
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_NewLateBinding,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateIndexSetComplex
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_NewLateBinding,                                // DeclaringTypeId
                0,                                                                                                          // Arity
                    5,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // Microsoft_VisualBasic_CompilerServices_StandardModuleAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_StandardModuleAttribute,                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // Microsoft_VisualBasic_CompilerServices_StaticLocalInitFlag__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_StaticLocalInitFlag,                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // Microsoft_VisualBasic_CompilerServices_StaticLocalInitFlag__State
                (ushort)MemberFlags.Field,                                                                                  // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_StaticLocalInitFlag,                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,                                     // Field Signature

                // Microsoft_VisualBasic_CompilerServices_StringType__MidStmtStr
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_StringType,                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_IncompleteInitialization__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_IncompleteInitialization,                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // Microsoft_VisualBasic_Embedded__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Embedded,                                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // Microsoft_VisualBasic_CompilerServices_Utils__CopyArray
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Utils,                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Array,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Array,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Array,

                // Microsoft_VisualBasic_CompilerServices_LikeOperator__LikeStringStringStringCompareMethod
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_LikeOperator,                                  // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_VisualBasic_CompareMethod,

                // Microsoft_VisualBasic_CompilerServices_LikeOperator__LikeObjectObjectObjectCompareMethod
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_LikeOperator,                                  // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_VisualBasic_CompareMethod,

                // Microsoft_VisualBasic_CompilerServices_ProjectData__CreateProjectError
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ProjectData,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Exception,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // Microsoft_VisualBasic_CompilerServices_ProjectData__SetProjectError
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ProjectData,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Exception,

                // Microsoft_VisualBasic_CompilerServices_ProjectData__SetProjectError_Int32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ProjectData,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Exception,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // Microsoft_VisualBasic_CompilerServices_ProjectData__ClearProjectError
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ProjectData,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // Microsoft_VisualBasic_CompilerServices_ProjectData__EndApp
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ProjectData,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // Microsoft_VisualBasic_CompilerServices_ObjectFlowControl_ForLoopControl__ForLoopInitObj
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ObjectFlowControl_ForLoopControl,              // DeclaringTypeId
                0,                                                                                                          // Arity
                    6,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_ObjectFlowControl_ForLoopControl__ForNextCheckObj
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ObjectFlowControl_ForLoopControl,              // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_ObjectFlowControl__CheckForSyncLockOnValueType
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_ObjectFlowControl,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Versioned__CallByName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Versioned,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_VisualBasic_CallType,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Versioned__IsNumeric
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Versioned,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Versioned__SystemTypeName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Versioned,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_CompilerServices_Versioned__TypeName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Versioned,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_CompilerServices_Versioned__VbTypeName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_CompilerServices_Versioned,                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_Information__IsNumeric
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Information,                                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_Information__SystemTypeName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Information,                                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_Information__TypeName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Information,                                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // Microsoft_VisualBasic_Information__VbTypeName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Information,                                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_Interaction__CallByName
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Interaction,                                                    // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.Microsoft_VisualBasic_CallType,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_IAsyncStateMachine_MoveNext
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_IAsyncStateMachine,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_IAsyncStateMachine_SetStateMachine
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_IAsyncStateMachine,                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_IAsyncStateMachine,

                // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__SetException
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncVoidMethodBuilder,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Exception,

                // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__SetResult
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncVoidMethodBuilder,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__AwaitOnCompleted
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncVoidMethodBuilder,                               // DeclaringTypeId
                2,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__AwaitUnsafeOnCompleted
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncVoidMethodBuilder,                               // DeclaringTypeId
                2,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__Start_T
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncVoidMethodBuilder,                               // DeclaringTypeId
                1,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__SetStateMachine
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncVoidMethodBuilder,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_IAsyncStateMachine,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__SetException
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Exception,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__SetResult
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__AwaitOnCompleted
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder,                               // DeclaringTypeId
                2,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__AwaitUnsafeOnCompleted
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder,                               // DeclaringTypeId
                2,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__Start_T
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder,                               // DeclaringTypeId
                1,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__SetStateMachine
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_IAsyncStateMachine,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__Task
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder,                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Threading_Tasks_Task,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__SetException
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Exception,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__SetResult
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__AwaitOnCompleted
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T,                             // DeclaringTypeId
                2,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__AwaitUnsafeOnCompleted
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T,                             // DeclaringTypeId
                2,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, (ushort)SpecialType.System_Object,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__Start_T
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T,                             // DeclaringTypeId
                1,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.ByReference, (ushort)SignatureTypeCode.GenericMethodParameter, 0,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__SetStateMachine
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_CompilerServices_IAsyncStateMachine,

                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__Task
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T,                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Threading_Tasks_Task_T,
                    1,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Runtime_CompilerServices_AsyncStateMachineAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_AsyncStateMachineAttribute,                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // System_Runtime_CompilerServices_IteratorStateMachineAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Runtime_CompilerServices_IteratorStateMachineAttribute,                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // Microsoft_VisualBasic_Strings__AscCharInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Strings,                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,

                // Microsoft_VisualBasic_Strings__AscStringInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Strings,                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_Strings__AscWCharInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Strings,                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,

                // Microsoft_VisualBasic_Strings__AscWStringInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Strings,                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // Microsoft_VisualBasic_Strings__ChrInt32Char
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Strings,                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // Microsoft_VisualBasic_Strings__ChrWInt32Char
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.Microsoft_VisualBasic_Strings,                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Xml_Linq_XElement__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Xml_Linq_XElement,                                                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Xml_Linq_XName,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Xml_Linq_XElement__ctor2
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_Xml_Linq_XElement,                                                             // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Xml_Linq_XName,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Xml_Linq_XNamespace__Get
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Xml_Linq_XNamespace,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Xml_Linq_XNamespace,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Windows_Forms_Application__RunForm
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.System_Windows_Forms_Application,                                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Windows_Forms_Form,

                // System_Environment__CurrentManagedThreadId
                (ushort)(MemberFlags.Property | MemberFlags.Static),                                                        // Flags
                (ushort)WellKnownType.System_Environment,                                                                   // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_ComponentModel_EditorBrowsableAttribute__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)WellKnownType.System_ComponentModel_EditorBrowsableAttribute,                                       // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_ComponentModel_EditorBrowsableState,

                // System_Runtime_GCLatencyMode__SustainedLowLatency
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)WellKnownType.System_Runtime_GCLatencyMode,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Runtime_GCLatencyMode,                   // Field Signature

                // System_String__Format_IFormatProvider
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_IFormatProvider,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // CSharp_Meta_MetaPrimitives__AddTrait
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // CSharp_Meta_MetaPrimitives__AddTrait_T
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                1,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,

                // CSharp_Meta_MetaPrimitives__ApplyDecorator
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MemberInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.CSharp_Meta_Decorator,

                // CSharp_Meta_MetaPrimitives__CloneArguments
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // CSharp_Meta_MetaPrimitives__CloneArgumentsToObjectArray
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // CSharp_Meta_MetaPrimitives__ParameterType
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodBase,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // CSharp_Meta_MetaPrimitives__ParameterType2
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_PropertyInfo,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // CSharp_Meta_MetaPrimitives__ThisObjectType
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_MethodBase,

                // CSharp_Meta_MetaPrimitives__ThisObjectType2
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)WellKnownType.CSharp_Meta_MetaPrimitives,                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Type,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)WellKnownType.System_Reflection_PropertyInfo,
            };

            string[] allNames = new string[(int)WellKnownMember.Count]
            {
                "Round",                                    // System_Math__RoundDouble
                "Pow",                                      // System_Math__PowDoubleDouble
                "get_Length",                               // System_Array__get_Length
                "Empty",                                    // System_Array__Empty
                "Length",                                   // System_Array__Length
                "ToBoolean",                                // System_Convert__ToBooleanDecimal
                "ToBoolean",                                // System_Convert__ToBooleanInt32
                "ToBoolean",                                // System_Convert__ToBooleanUInt32
                "ToBoolean",                                // System_Convert__ToBooleanInt64
                "ToBoolean",                                // System_Convert__ToBooleanUInt64
                "ToBoolean",                                // System_Convert__ToBooleanSingle
                "ToBoolean",                                // System_Convert__ToBooleanDouble
                "ToSByte",                                  // System_Convert__ToSByteDecimal
                "ToSByte",                                  // System_Convert__ToSByteDouble
                "ToSByte",                                  // System_Convert__ToSByteSingle
                "ToByte",                                   // System_Convert__ToByteDecimal
                "ToByte",                                   // System_Convert__ToByteDouble
                "ToByte",                                   // System_Convert__ToByteSingle
                "ToInt16",                                  // System_Convert__ToInt16Decimal
                "ToInt16",                                  // System_Convert__ToInt16Double
                "ToInt16",                                  // System_Convert__ToInt16Single
                "ToUInt16",                                 // System_Convert__ToUInt16Decimal
                "ToUInt16",                                 // System_Convert__ToUInt16Double
                "ToUInt16",                                 // System_Convert__ToUInt16Single
                "ToInt32",                                  // System_Convert__ToInt32Decimal
                "ToInt32",                                  // System_Convert__ToInt32Double
                "ToInt32",                                  // System_Convert__ToInt32Single
                "ToUInt32",                                 // System_Convert__ToUInt32Decimal
                "ToUInt32",                                 // System_Convert__ToUInt32Double
                "ToUInt32",                                 // System_Convert__ToUInt32Single
                "ToInt64",                                  // System_Convert__ToInt64Decimal
                "ToInt64",                                  // System_Convert__ToInt64Double
                "ToInt64",                                  // System_Convert__ToInt64Single
                "ToUInt64",                                 // System_Convert__ToUInt64Decimal
                "ToUInt64",                                 // System_Convert__ToUInt64Double
                "ToUInt64",                                 // System_Convert__ToUInt64Single
                "ToSingle",                                 // System_Convert__ToSingleDecimal
                "ToDouble",                                 // System_Convert__ToDoubleDecimal
                ".ctor",                                    // System_CLSCompliantAttribute__ctor
                ".ctor",                                    // System_FlagsAttribute__ctor
                ".ctor",                                    // System_Guid__ctor
                "FullName",                                 // System_Type__FullName
                "GetConstructors",                          // System_Type__GetConstructors
                "GetConstructors",                          // System_Type__GetConstructors2
                "GetMethods",                               // System_Type__GetMethods
                "GetMethods",                               // System_Type__GetMethods2
                "GetProperties",                            // System_Type__GetProperties
                "GetProperties",                            // System_Type__GetProperties2
                "GetProperty",                              // System_Type__GetProperty
                "GetTypeFromCLSID",                         // System_Type__GetTypeFromCLSID
                "GetTypeFromHandle",                        // System_Type__GetTypeFromHandle
                "IsAbstract",                               // System_Type__IsAbstract
                "IsArray",                                  // System_Type__IsArray
                "IsAssignableFrom",                         // System_Type__IsAssignableFrom
                "IsByRef",                                  // System_Type__IsByRef
                "IsClass",                                  // System_Type__IsClass
                "IsConstructedGenericType",                 // System_Type__IsConstructedGenericType
                "IsEnum",                                   // System_Type__IsEnum
                "IsGenericParameter",                       // System_Type__IsGenericParameter
                "IsGenericType",                            // System_Type__IsGenericType
                "IsGenericTypeDefinition",                  // System_Type__IsGenericTypeDefinition
                "IsInterface",                              // System_Type__IsInterface
                "IsNested",                                 // System_Type__IsNested
                "IsNestedAssembly",                         // System_Type__IsNestedAssembly
                "IsNestedPrivate",                          // System_Type__IsNestedPrivate
                "IsNestedPublic",                           // System_Type__IsNestedPublic
                "IsNotPublic",                              // System_Type__IsNotPublic
                "IsPublic",                                 // System_Type__IsPublic
                "IsSealed",                                 // System_Type__IsSealed
                "IsValueType",                              // System_Type__IsValueType
                "IsVisible",                                // System_Type__IsVisible
                "Missing",                                  // System_Type__Missing
                "Namespace",                                // System_Type__Namespace
                ".ctor",                                    // System_Reflection_AssemblyKeyFileAttribute__ctor
                ".ctor",                                    // System_Reflection_AssemblyKeyNameAttribute__ctor
                "DeclaringType",                            // System_Reflection_MemberInfo__DeclaringType
                "Name",                                     // System_Reflection_MemberInfo__Name
                "GetMethodFromHandle",                      // System_Reflection_MethodBase__GetMethodFromHandle
                "GetMethodFromHandle",                      // System_Reflection_MethodBase__GetMethodFromHandle2
                "GetParameters",                            // System_Reflection_MethodBase__GetParameters
                "Invoke",                                   // System_Reflection_MethodBase__Invoke
                "IsStatic",                                 // System_Reflection_MethodBase__IsStatic
                "CreateDelegate",                           // System_Reflection_MethodInfo__CreateDelegate
                "ReturnType",                               // System_Reflection_MethodInfo__ReturnType
                "IsOut",                                    // System_Reflection_ParameterInfo__IsOut
                "Member",                                   // System_Reflection_ParameterInfo__Member
                "Name",                                     // System_Reflection_ParameterInfo__Name
                "ParameterType",                            // System_Reflection_ParameterInfo__ParameterType
                "Position",                                 // System_Reflection_ParameterInfo__Position
                "GetAccessors",                             // System_Reflection_PropertyInfo__GetAccessors
                "GetValue",                                 // System_Reflection_PropertyInfo__GetValue
                "GetValue",                                 // System_Reflection_PropertyInfo__GetValue2
                "PropertyType",                             // System_Reflection_PropertyInfo__PropertyType
                "SetValue",                                 // System_Reflection_PropertyInfo__SetValue
                "SetValue",                                 // System_Reflection_PropertyInfo__SetValue2
                "CreateDelegate",                           // System_Delegate__CreateDelegate
                "CreateDelegate",                           // System_Delegate__CreateDelegate4
                "GetFieldFromHandle",                       // System_Reflection_FieldInfo__GetFieldFromHandle
                "GetFieldFromHandle",                       // System_Reflection_FieldInfo__GetFieldFromHandle2
                "Value",                                    // System_Reflection_Missing__Value
                "GetCustomAttribute",                       // System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T
                "GetCustomAttribute",                       // System_Reflection_CustomAttributeExtensions__GetCustomAttribute_T2
                "Equals",                                   // System_IEquatable_T__Equals
                "Equals",                                   // System_Collections_Generic_EqualityComparer_T__Equals
                "GetHashCode",                              // System_Collections_Generic_EqualityComparer_T__GetHashCode
                "get_Default",                              // System_Collections_Generic_EqualityComparer_T__get_Default
                ".ctor",                                    // System_AttributeUsageAttribute__ctor
                "AllowMultiple",                            // System_AttributeUsageAttribute__AllowMultiple
                "Inherited",                                // System_AttributeUsageAttribute__Inherited
                ".ctor",                                    // System_ParamArrayAttribute__ctor
                ".ctor",                                    // System_STAThreadAttribute__ctor
                ".ctor",                                    // System_Reflection_DefaultMemberAttribute__ctor
                "Break",                                    // System_Diagnostics_Debugger__Break
                ".ctor",                                    // System_Diagnostics_DebuggerDisplayAttribute__ctor
                "Type",                                     // System_Diagnostics_DebuggerDisplayAttribute__Type
                ".ctor",                                    // System_Diagnostics_DebuggerNonUserCodeAttribute__ctor
                ".ctor",                                    // System_Diagnostics_DebuggerHiddenAttribute__ctor
                ".ctor",                                    // System_Diagnostics_DebuggerBrowsableAttribute__ctor
                ".ctor",                                    // System_Diagnostics_DebuggerStepThroughAttribute__ctor
                ".ctor",                                    // System_Diagnostics_DebuggableAttribute__ctorDebuggingModes
                "Default",                                  // System_Diagnostics_DebuggableAttribute_DebuggingModes__Default
                "DisableOptimizations",                     // System_Diagnostics_DebuggableAttribute_DebuggingModes__DisableOptimizations
                "EnableEditAndContinue",                    // System_Diagnostics_DebuggableAttribute_DebuggingModes__EnableEditAndContinue
                "IgnoreSymbolStoreSequencePoints",          // System_Diagnostics_DebuggableAttribute_DebuggingModes__IgnoreSymbolStoreSequencePoints
                ".ctor",                                    // System_Runtime_InteropServices_UnknownWrapper__ctor
                ".ctor",                                    // System_Runtime_InteropServices_DispatchWrapper__ctor
                ".ctor",                                    // System_Runtime_InteropServices_ClassInterfaceAttribute__ctorClassInterfaceType
                ".ctor",                                    // System_Runtime_InteropServices_CoClassAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_ComAwareEventInfo__ctor
                "AddEventHandler",                          // System_Runtime_InteropServices_ComAwareEventInfo__AddEventHandler
                "RemoveEventHandler",                       // System_Runtime_InteropServices_ComAwareEventInfo__RemoveEventHandler
                ".ctor",                                    // System_Runtime_InteropServices_ComEventInterfaceAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_ComSourceInterfacesAttribute__ctorString
                ".ctor",                                    // System_Runtime_InteropServices_ComVisibleAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_DispIdAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_GuidAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_InterfaceTypeAttribute__ctorComInterfaceType
                ".ctor",                                    // System_Runtime_InteropServices_InterfaceTypeAttribute__ctorInt16
                "GetTypeFromCLSID",                         // System_Runtime_InteropServices_Marshal__GetTypeFromCLSID
                ".ctor",                                    // System_Runtime_InteropServices_TypeIdentifierAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_TypeIdentifierAttribute__ctorStringString
                ".ctor",                                    // System_Runtime_InteropServices_BestFitMappingAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_DefaultParameterValueAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_LCIDConversionAttribute__ctor
                ".ctor",                                    // System_Runtime_InteropServices_UnmanagedFunctionPointerAttribute__ctor
                "AddEventHandler",                          // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__AddEventHandler
                "GetOrCreateEventRegistrationTokenTable",   // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__GetOrCreateEventRegistrationTokenTable
                "InvocationList",                           // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__InvocationList
                "RemoveEventHandler",                       // System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__RemoveEventHandler
                "AddEventHandler",                          // System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal__AddEventHandler_T
                "RemoveAllEventHandlers",                   // System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal__RemoveAllEventHandlers
                "RemoveEventHandler",                       // System_Runtime_InteropServices_WindowsRuntime_WindowsRuntimeMarshal__RemoveEventHandler_T
                ".ctor",                                    // System_Runtime_CompilerServices_DateTimeConstantAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_DecimalConstantAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_DecimalConstantAttribute__ctorByteByteInt32Int32Int32
                ".ctor",                                    // System_Runtime_CompilerServices_ExtensionAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_AccessedThroughPropertyAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_CompilationRelaxationsAttribute__ctorInt32
                ".ctor",                                    // System_Runtime_CompilerServices_RuntimeCompatibilityAttribute__ctor
                "WrapNonExceptionThrows",                   // System_Runtime_CompilerServices_RuntimeCompatibilityAttribute__WrapNonExceptionThrows
                ".ctor",                                    // System_Runtime_CompilerServices_UnsafeValueTypeAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_FixedBufferAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_DynamicAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_DynamicAttribute__ctorTransformFlags
                "Create",                                   // System_Runtime_CompilerServices_CallSite_T__Create
                "Target",                                   // System_Runtime_CompilerServices_CallSite_T__Target
                "GetObjectValue",                           // System_Runtime_CompilerServices_RuntimeHelpers__GetObjectValueObject
                "InitializeArray",                          // System_Runtime_CompilerServices_RuntimeHelpers__InitializeArrayArrayRuntimeFieldHandle
                "get_OffsetToStringData",                   // System_Runtime_CompilerServices_RuntimeHelpers__get_OffsetToStringData
                "Capture",                                  // System_Runtime_ExceptionServices_ExceptionDispatchInfo__Capture
                "Throw",                                    // System_Runtime_ExceptionServices_ExceptionDispatchInfo__Throw
                ".ctor",                                    // System_Security_UnverifiableCodeAttribute__ctor
                "RequestMinimum",                           // System_Security_Permissions_SecurityAction__RequestMinimum
                ".ctor",                                    // System_Security_Permissions_SecurityPermissionAttribute__ctor
                "SkipVerification",                         // System_Security_Permissions_SecurityPermissionAttribute__SkipVerification
                "CreateInstance",                           // System_Activator__CreateInstance
                "CreateInstance",                           // System_Activator__CreateInstance_T
                "CompareExchange",                          // System_Threading_Interlocked__CompareExchange_T
                "Enter",                                    // System_Threading_Monitor__Enter
                "Enter",                                    // System_Threading_Monitor__Enter2
                "Exit",                                     // System_Threading_Monitor__Exit
                "CurrentThread",                            // System_Threading_Thread__CurrentThread
                "ManagedThreadId",                          // System_Threading_Thread__ManagedThreadId
                "BinaryOperation",                          // Microsoft_CSharp_RuntimeBinder_Binder__BinaryOperation
                "Convert",                                  // Microsoft_CSharp_RuntimeBinder_Binder__Convert
                "GetIndex",                                 // Microsoft_CSharp_RuntimeBinder_Binder__GetIndex
                "GetMember",                                // Microsoft_CSharp_RuntimeBinder_Binder__GetMember
                "Invoke",                                   // Microsoft_CSharp_RuntimeBinder_Binder__Invoke
                "InvokeConstructor",                        // Microsoft_CSharp_RuntimeBinder_Binder__InvokeConstructor
                "InvokeMember",                             // Microsoft_CSharp_RuntimeBinder_Binder__InvokeMember
                "IsEvent",                                  // Microsoft_CSharp_RuntimeBinder_Binder__IsEvent
                "SetIndex",                                 // Microsoft_CSharp_RuntimeBinder_Binder__SetIndex
                "SetMember",                                // Microsoft_CSharp_RuntimeBinder_Binder__SetMember
                "UnaryOperation",                           // Microsoft_CSharp_RuntimeBinder_Binder__UnaryOperation
                "Create",                                   // Microsoft_CSharp_RuntimeBinder_CSharpArgumentInfo__Create
                "ToDecimal",                                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDecimalBoolean
                "ToBoolean",                                // Microsoft_VisualBasic_CompilerServices_Conversions__ToBooleanString
                "ToSByte",                                  // Microsoft_VisualBasic_CompilerServices_Conversions__ToSByteString
                "ToByte",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToByteString
                "ToShort",                                  // Microsoft_VisualBasic_CompilerServices_Conversions__ToShortString
                "ToUShort",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToUShortString
                "ToInteger",                                // Microsoft_VisualBasic_CompilerServices_Conversions__ToIntegerString
                "ToUInteger",                               // Microsoft_VisualBasic_CompilerServices_Conversions__ToUIntegerString
                "ToLong",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToLongString
                "ToULong",                                  // Microsoft_VisualBasic_CompilerServices_Conversions__ToULongString
                "ToSingle",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToSingleString
                "ToDouble",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToDoubleString
                "ToDecimal",                                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDecimalString
                "ToDate",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToDateString
                "ToChar",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharString
                "ToCharArrayRankOne",                       // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharArrayRankOneString
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringBoolean
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringInt32
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringByte
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringUInt32
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringInt64
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringUInt64
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringSingle
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringDouble
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringDecimal
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringDateTime
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringChar
                "ToString",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToStringObject
                "ToBoolean",                                // Microsoft_VisualBasic_CompilerServices_Conversions__ToBooleanObject
                "ToSByte",                                  // Microsoft_VisualBasic_CompilerServices_Conversions__ToSByteObject
                "ToByte",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToByteObject
                "ToShort",                                  // Microsoft_VisualBasic_CompilerServices_Conversions__ToShortObject
                "ToUShort",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToUShortObject
                "ToInteger",                                // Microsoft_VisualBasic_CompilerServices_Conversions__ToIntegerObject
                "ToUInteger",                               // Microsoft_VisualBasic_CompilerServices_Conversions__ToUIntegerObject
                "ToLong",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToLongObject
                "ToULong",                                  // Microsoft_VisualBasic_CompilerServices_Conversions__ToULongObject
                "ToSingle",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToSingleObject
                "ToDouble",                                 // Microsoft_VisualBasic_CompilerServices_Conversions__ToDoubleObject
                "ToDecimal",                                // Microsoft_VisualBasic_CompilerServices_Conversions__ToDecimalObject
                "ToDate",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToDateObject
                "ToChar",                                   // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharObject
                "ToCharArrayRankOne",                       // Microsoft_VisualBasic_CompilerServices_Conversions__ToCharArrayRankOneObject
                "ToGenericParameter",                       // Microsoft_VisualBasic_CompilerServices_Conversions__ToGenericParameter_T_Object
                "ChangeType",                               // Microsoft_VisualBasic_CompilerServices_Conversions__ChangeType
                "PlusObject",                               // Microsoft_VisualBasic_CompilerServices_Operators__PlusObjectObject
                "NegateObject",                             // Microsoft_VisualBasic_CompilerServices_Operators__NegateObjectObject
                "NotObject",                                // Microsoft_VisualBasic_CompilerServices_Operators__NotObjectObject
                "AndObject",                                // Microsoft_VisualBasic_CompilerServices_Operators__AndObjectObjectObject
                "OrObject",                                 // Microsoft_VisualBasic_CompilerServices_Operators__OrObjectObjectObject
                "XorObject",                                // Microsoft_VisualBasic_CompilerServices_Operators__XorObjectObjectObject
                "AddObject",                                // Microsoft_VisualBasic_CompilerServices_Operators__AddObjectObjectObject
                "SubtractObject",                           // Microsoft_VisualBasic_CompilerServices_Operators__SubtractObjectObjectObject
                "MultiplyObject",                           // Microsoft_VisualBasic_CompilerServices_Operators__MultiplyObjectObjectObject
                "DivideObject",                             // Microsoft_VisualBasic_CompilerServices_Operators__DivideObjectObjectObject
                "ExponentObject",                           // Microsoft_VisualBasic_CompilerServices_Operators__ExponentObjectObjectObject
                "ModObject",                                // Microsoft_VisualBasic_CompilerServices_Operators__ModObjectObjectObject
                "IntDivideObject",                          // Microsoft_VisualBasic_CompilerServices_Operators__IntDivideObjectObjectObject
                "LeftShiftObject",                          // Microsoft_VisualBasic_CompilerServices_Operators__LeftShiftObjectObjectObject
                "RightShiftObject",                         // Microsoft_VisualBasic_CompilerServices_Operators__RightShiftObjectObjectObject
                "ConcatenateObject",                        // Microsoft_VisualBasic_CompilerServices_Operators__ConcatenateObjectObjectObject
                "CompareObjectEqual",                       // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectEqualObjectObjectBoolean
                "CompareObjectNotEqual",                    // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectNotEqualObjectObjectBoolean
                "CompareObjectLess",                        // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectLessObjectObjectBoolean
                "CompareObjectLessEqual",                   // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectLessEqualObjectObjectBoolean
                "CompareObjectGreaterEqual",                // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectGreaterEqualObjectObjectBoolean
                "CompareObjectGreater",                     // Microsoft_VisualBasic_CompilerServices_Operators__CompareObjectGreaterObjectObjectBoolean
                "ConditionalCompareObjectEqual",            // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectEqualObjectObjectBoolean
                "ConditionalCompareObjectNotEqual",         // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectNotEqualObjectObjectBoolean
                "ConditionalCompareObjectLess",             // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectLessObjectObjectBoolean
                "ConditionalCompareObjectLessEqual",        // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectLessEqualObjectObjectBoolean
                "ConditionalCompareObjectGreaterEqual",     // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectGreaterEqualObjectObjectBoolean
                "ConditionalCompareObjectGreater",          // Microsoft_VisualBasic_CompilerServices_Operators__ConditionalCompareObjectGreaterObjectObjectBoolean
                "CompareString",                            // Microsoft_VisualBasic_CompilerServices_Operators__CompareStringStringStringBoolean
                "CompareString",                            // Microsoft_VisualBasic_CompilerServices_EmbeddedOperators__CompareStringStringStringBoolean
                "LateCall",                                 // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateCall
                "LateGet",                                  // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateGet
                "LateSet",                                  // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateSet
                "LateSetComplex",                           // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateSetComplex
                "LateIndexGet",                             // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateIndexGet
                "LateIndexSet",                             // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateIndexSet
                "LateIndexSetComplex",                      // Microsoft_VisualBasic_CompilerServices_NewLateBinding__LateIndexSetComplex
                ".ctor",                                    // Microsoft_VisualBasic_CompilerServices_StandardModuleAttribute__ctor
                ".ctor",                                    // Microsoft_VisualBasic_CompilerServices_StaticLocalInitFlag__ctor
                "State",                                    // Microsoft_VisualBasic_CompilerServices_StaticLocalInitFlag__State
                "MidStmtStr",                               // Microsoft_VisualBasic_CompilerServices_StringType__MidStmtStr
                ".ctor",                                    // Microsoft_VisualBasic_CompilerServices_IncompleteInitialization__ctor
                ".ctor",                                    // Microsoft_VisualBasic_Embedded__ctor
                "CopyArray",                                // Microsoft_VisualBasic_CompilerServices_Utils__CopyArray
                "LikeString",                               // Microsoft_VisualBasic_CompilerServices_LikeOperator__LikeStringStringStringCompareMethod
                "LikeObject",                               // Microsoft_VisualBasic_CompilerServices_LikeOperator__LikeObjectObjectObjectCompareMethod
                "CreateProjectError",                       // Microsoft_VisualBasic_CompilerServices_ProjectData__CreateProjectError
                "SetProjectError",                          // Microsoft_VisualBasic_CompilerServices_ProjectData__SetProjectError
                "SetProjectError",                          // Microsoft_VisualBasic_CompilerServices_ProjectData__SetProjectError_Int32
                "ClearProjectError",                        // Microsoft_VisualBasic_CompilerServices_ProjectData__ClearProjectError
                "EndApp",                                   // Microsoft_VisualBasic_CompilerServices_ProjectData__EndApp
                "ForLoopInitObj",                           // Microsoft_VisualBasic_CompilerServices_ObjectFlowControl_ForLoopControl__ForLoopInitObj
                "ForNextCheckObj",                          // Microsoft_VisualBasic_CompilerServices_ObjectFlowControl_ForLoopControl__ForNextCheckObj
                "CheckForSyncLockOnValueType",              // Microsoft_VisualBasic_CompilerServices_ObjectFlowControl__CheckForSyncLockOnValueType
                "CallByName",                               // Microsoft_VisualBasic_CompilerServices_Versioned__CallByName
                "IsNumeric",                                // Microsoft_VisualBasic_CompilerServices_Versioned__IsNumeric
                "SystemTypeName",                           // Microsoft_VisualBasic_CompilerServices_Versioned__SystemTypeName
                "TypeName",                                 // Microsoft_VisualBasic_CompilerServices_Versioned__TypeName
                "VbTypeName",                               // Microsoft_VisualBasic_CompilerServices_Versioned__VbTypeName
                "IsNumeric",                                // Microsoft_VisualBasic_Information__IsNumeric
                "SystemTypeName",                           // Microsoft_VisualBasic_Information__SystemTypeName
                "TypeName",                                 // Microsoft_VisualBasic_Information__TypeName
                "VbTypeName",                               // Microsoft_VisualBasic_Information__VbTypeName
                "CallByName",                               // Microsoft_VisualBasic_Interaction__CallByName
                "MoveNext",                                 // System_Runtime_CompilerServices_IAsyncStateMachine_MoveNext
                "SetStateMachine",                          // System_Runtime_CompilerServices_IAsyncStateMachine_SetStateMachine
                "SetException",                             // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__SetException
                "SetResult",                                // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__SetResult
                "AwaitOnCompleted",                         // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__AwaitOnCompleted
                "AwaitUnsafeOnCompleted",                   // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__AwaitUnsafeOnCompleted
                "Start",                                    // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__Start_T
                "SetStateMachine",                          // System_Runtime_CompilerServices_AsyncVoidMethodBuilder__SetStateMachine
                "SetException",                             // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__SetException
                "SetResult",                                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__SetResult
                "AwaitOnCompleted",                         // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__AwaitOnCompleted
                "AwaitUnsafeOnCompleted",                   // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__AwaitUnsafeOnCompleted
                "Start",                                    // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__Start_T
                "SetStateMachine",                          // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__SetStateMachine
                "Task",                                     // System_Runtime_CompilerServices_AsyncTaskMethodBuilder__Task
                "SetException",                             // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__SetException
                "SetResult",                                // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__SetResult
                "AwaitOnCompleted",                         // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__AwaitOnCompleted
                "AwaitUnsafeOnCompleted",                   // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__AwaitUnsafeOnCompleted
                "Start",                                    // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__Start_T
                "SetStateMachine",                          // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__SetStateMachine
                "Task",                                     // System_Runtime_CompilerServices_AsyncTaskMethodBuilder_T__Task
                ".ctor",                                    // System_Runtime_CompilerServices_AsyncStateMachineAttribute__ctor
                ".ctor",                                    // System_Runtime_CompilerServices_IteratorStateMachineAttribute__ctor
                "Asc",                                      // Microsoft_VisualBasic_Strings__AscCharInt32
                "Asc",                                      // Microsoft_VisualBasic_Strings__AscStringInt32
                "AscW",                                     // Microsoft_VisualBasic_Strings__AscWCharInt32
                "AscW",                                     // Microsoft_VisualBasic_Strings__AscWStringInt32
                "Chr",                                      // Microsoft_VisualBasic_Strings__ChrInt32Char
                "ChrW",                                     // Microsoft_VisualBasic_Strings__ChrWInt32Char
                ".ctor",                                    // System_Xml_Linq_XElement__ctor
                ".ctor",                                    // System_Xml_Linq_XElement__ctor2
                "Get",                                      // System_Xml_Linq_XNamespace__Get
                "Run",                                      // System_Windows_Forms_Application__RunForm
                "CurrentManagedThreadId",                   // System_Environment__CurrentManagedThreadId
                ".ctor",                                    // System_ComponentModel_EditorBrowsableAttribute__ctor
                "SustainedLowLatency",                      // System_Runtime_GCLatencyMode__SustainedLowLatency
                "Format",                                   // System_String__Format_IFormatProvider
                "AddTrait",                                 // CSharp_Meta_MetaPrimitives__AddTrait
                "AddTrait",                                 // CSharp_Meta_MetaPrimitives__AddTrait_T
                "ApplyDecorator",                           // CSharp_Meta_MetaPrimitives__ApplyDecorator
                "CloneArguments",                           // CSharp_Meta_MetaPrimitives__CloneArguments
                "CloneArgumentsToObjectArray",              // CSharp_Meta_MetaPrimitives__CloneArgumentsToObjectArray
                "ParameterType",                            // CSharp_Meta_MetaPrimitives__ParameterType
                "ParameterType",                            // CSharp_Meta_MetaPrimitives__ParameterType2
                "ThisObjectType",                           // CSharp_Meta_MetaPrimitives__ThisObjectType
                "ThisObjectType",                           // CSharp_Meta_MetaPrimitives__ThisObjectType2
            };

            using (var memoryStream = new MemoryStream(initializationValues.Length * 2))
            {
                byte[] buffer;
                for (int i = 0; i < initializationValues.Length; i++)
                {
                    buffer = BitConverter.GetBytes(initializationValues[i]);
                    memoryStream.Write(buffer, 0, 2);
                }
                memoryStream.Seek(0, SeekOrigin.Begin);
                s_descriptors = MemberDescriptor.InitializeFromStream(memoryStream, allNames);
            }
        }

        public static MemberDescriptor GetDescriptor(WellKnownMember member)
        {
            return s_descriptors[(int)member];
        }

        /// <summary>
        /// This function defines whether an attribute is optional or not.
        /// </summary>
        /// <param name="attributeMember">The attribute member.</param>
        internal static bool IsSynthesizedAttributeOptional(WellKnownMember attributeMember)
        {
            switch (attributeMember)
            {
                case WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor:
                case WellKnownMember.System_Diagnostics_DebuggableAttribute__ctorDebuggingModes:
                case WellKnownMember.System_Diagnostics_DebuggerBrowsableAttribute__ctor:
                case WellKnownMember.System_Diagnostics_DebuggerHiddenAttribute__ctor:
                case WellKnownMember.System_Diagnostics_DebuggerDisplayAttribute__ctor:
                case WellKnownMember.System_Diagnostics_DebuggerStepThroughAttribute__ctor:
                case WellKnownMember.System_Diagnostics_DebuggerNonUserCodeAttribute__ctor:
                case WellKnownMember.System_STAThreadAttribute__ctor:
                case WellKnownMember.System_Runtime_CompilerServices_AsyncStateMachineAttribute__ctor:
                case WellKnownMember.System_Runtime_CompilerServices_IteratorStateMachineAttribute__ctor:
                    return true;

                default:
                    return false;
            }
        }
    }
}
