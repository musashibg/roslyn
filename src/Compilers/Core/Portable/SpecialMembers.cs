// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.RuntimeMembers;
using System.IO;
using System;

namespace Microsoft.CodeAnalysis
{
    internal static class SpecialMembers
    {
        private readonly static ImmutableArray<MemberDescriptor> s_descriptors;

        static SpecialMembers()
        {
            ushort[] initializationValues = new ushort[]
            {
                // System_String__CtorSZArrayChar
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,

                // System_String__ConcatStringString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_String__ConcatStringStringString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_String__ConcatStringStringStringString
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    4,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_String__ConcatStringArray
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_String__ConcatObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_String__ConcatObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_String__ConcatObjectObjectObject
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    3,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_String__ConcatObjectArray
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_String__op_Equality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_String__op_Inequality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_String__Length
                (ushort)MemberFlags.PropertyGet,                                                                            // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_String__Chars
                (ushort)MemberFlags.PropertyGet,                                                                            // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_String__Format
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_String,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,
                    (ushort)SignatureTypeCode.SZArray, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Delegate__Combine
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Delegate,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,

                // System_Delegate__Remove
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Delegate,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,

                // System_Delegate__op_Equality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Delegate,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,

                // System_Delegate__op_Inequality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Delegate,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Delegate,

                // System_Decimal__Zero
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,                                   // Field Signature

                // System_Decimal__MinusOne
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,                                   // Field Signature

                // System_Decimal__One
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,                                   // Field Signature

                // System_Decimal__CtorInt32
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Decimal__CtorUInt32
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,

                // System_Decimal__CtorInt64
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // System_Decimal__CtorUInt64
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,

                // System_Decimal__CtorSingle
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_Decimal__CtorDouble
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Decimal__CtorInt32Int32Int32BooleanByte
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    5,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,

                // System_Decimal__op_Addition
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Subtraction
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Multiply
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Division
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Modulus
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_UnaryNegation
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Increment
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Decrement
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__NegateDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__RemainderDecimalDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__AddDecimalDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__SubtractDecimalDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__MultiplyDecimalDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__DivideDecimalDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__ModuloDecimalDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__CompareDecimalDecimal
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Equality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Inequality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_GreaterThan
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_GreaterThanOrEqual
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_LessThan
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_LessThanOrEqual
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Implicit_FromByte
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,

                // System_Decimal__op_Implicit_FromChar
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,

                // System_Decimal__op_Implicit_FromInt16
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,

                // System_Decimal__op_Implicit_FromInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Decimal__op_Implicit_FromInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // System_Decimal__op_Implicit_FromSByte
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_SByte,

                // System_Decimal__op_Implicit_FromUInt16
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt16,

                // System_Decimal__op_Implicit_FromUInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,

                // System_Decimal__op_Implicit_FromUInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,

                // System_Decimal__op_Explicit_ToByte
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Byte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToUInt16
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToSByte
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_SByte,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToInt16
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int16,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToSingle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToChar
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Char,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToUInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToUInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_ToInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,

                // System_Decimal__op_Explicit_FromDouble
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Double,

                // System_Decimal__op_Explicit_FromSingle
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Decimal,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Decimal,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Single,

                // System_DateTime__MinValue
                (ushort)(MemberFlags.Field | MemberFlags.Static),                                                           // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,                                  // Field Signature

                // System_DateTime__CtorInt64
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // System_DateTime__CompareDateTimeDateTime
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // System_DateTime__op_Equality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // System_DateTime__op_Inequality
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // System_DateTime__op_GreaterThan
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // System_DateTime__op_GreaterThanOrEqual
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // System_DateTime__op_LessThan
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // System_DateTime__op_LessThanOrEqual
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_DateTime,                                                                        // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_DateTime,

                // System_Collections_IEnumerable__GetEnumerator
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_Collections_IEnumerable,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_IEnumerator,

                // System_Collections_IEnumerator__Current
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)SpecialType.System_Collections_IEnumerator,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Collections_IEnumerator__get_Current
                (ushort)(MemberFlags.PropertyGet | MemberFlags.Virtual),                                                    // Flags
                (ushort)SpecialType.System_Collections_IEnumerator,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Collections_IEnumerator__MoveNext
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_Collections_IEnumerator,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Collections_IEnumerator__Reset
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_Collections_IEnumerator,                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Collections_Generic_IEnumerable_T__GetEnumerator
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_Collections_Generic_IEnumerable_T,                                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeInstance,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Collections_Generic_IEnumerator_T,
                    1,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Collections_Generic_IEnumerator_T__Current
                (ushort)(MemberFlags.Property | MemberFlags.Virtual),                                                       // Flags
                (ushort)SpecialType.System_Collections_Generic_IEnumerator_T,                                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Collections_Generic_IEnumerator_T__get_Current
                (ushort)(MemberFlags.PropertyGet | MemberFlags.Virtual),                                                    // Flags
                (ushort)SpecialType.System_Collections_Generic_IEnumerator_T,                                               // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_IDisposable__Dispose
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_IDisposable,                                                                     // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_Array__Length
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)SpecialType.System_Array,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Array__LongLength
                (ushort)MemberFlags.Property,                                                                               // Flags
                (ushort)SpecialType.System_Array,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // System_Array__GetLowerBound
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)SpecialType.System_Array,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Array__GetUpperBound
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)SpecialType.System_Array,                                                                           // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Object__GetHashCode
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_Object,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_Object__Equals
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_Object,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_Object__ToString
                (ushort)(MemberFlags.Method | MemberFlags.Virtual),                                                         // Flags
                (ushort)SpecialType.System_Object,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_String,

                // System_Object__ReferenceEquals
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Object,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    2,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Object,

                // System_IntPtr__op_Explicit_ToPointer
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_IntPtr,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.Pointer, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_IntPtr,

                // System_IntPtr__op_Explicit_ToInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_IntPtr,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_IntPtr,

                // System_IntPtr__op_Explicit_ToInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_IntPtr,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_IntPtr,

                // System_IntPtr__op_Explicit_FromPointer
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_IntPtr,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_IntPtr,
                    (ushort)SignatureTypeCode.Pointer, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_IntPtr__op_Explicit_FromInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_IntPtr,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_IntPtr,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int32,

                // System_IntPtr__op_Explicit_FromInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_IntPtr,                                                                          // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_IntPtr,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Int64,

                // System_UIntPtr__op_Explicit_ToPointer
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_UIntPtr,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.Pointer, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UIntPtr,

                // System_UIntPtr__op_Explicit_ToUInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_UIntPtr,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UIntPtr,

                // System_UIntPtr__op_Explicit_ToUInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_UIntPtr,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UIntPtr,

                // System_UIntPtr__op_Explicit_FromPointer
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_UIntPtr,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UIntPtr,
                    (ushort)SignatureTypeCode.Pointer, (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,

                // System_UIntPtr__op_Explicit_FromUInt32
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_UIntPtr,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UIntPtr,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt32,

                // System_UIntPtr__op_Explicit_FromUInt64
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_UIntPtr,                                                                         // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UIntPtr,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_UInt64,

                // System_Nullable_T_GetValueOrDefault
                (ushort)MemberFlags.Method,                                                                                 // Flags
                (ushort)SpecialType.System_Nullable_T,                                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Nullable_T_get_Value
                (ushort)MemberFlags.PropertyGet,                                                                            // Flags
                (ushort)SpecialType.System_Nullable_T,                                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Nullable_T_get_HasValue
                (ushort)MemberFlags.PropertyGet,                                                                            // Flags
                (ushort)SpecialType.System_Nullable_T,                                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    0,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Boolean,

                // System_Nullable_T__ctor
                (ushort)MemberFlags.Constructor,                                                                            // Flags
                (ushort)SpecialType.System_Nullable_T,                                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Void,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Nullable_T__op_Implicit_FromT
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Nullable_T,                                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Nullable_T,
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,

                // System_Nullable_T__op_Explicit_ToT
                (ushort)(MemberFlags.Method | MemberFlags.Static),                                                          // Flags
                (ushort)SpecialType.System_Nullable_T,                                                                      // DeclaringTypeId
                0,                                                                                                          // Arity
                    1,                                                                                                      // Method Signature
                    (ushort)SignatureTypeCode.GenericTypeParameter, 0,
                    (ushort)SignatureTypeCode.TypeHandle, (ushort)SpecialType.System_Nullable_T,
            };

            string[] allNames = new string[(int)SpecialMember.Count]
            {
                ".ctor",                                    // System_String__CtorSZArrayChar
                "Concat",                                   // System_String__ConcatStringString
                "Concat",                                   // System_String__ConcatStringStringString
                "Concat",                                   // System_String__ConcatStringStringStringString
                "Concat",                                   // System_String__ConcatStringArray
                "Concat",                                   // System_String__ConcatObject
                "Concat",                                   // System_String__ConcatObjectObject
                "Concat",                                   // System_String__ConcatObjectObjectObject
                "Concat",                                   // System_String__ConcatObjectArray
                "op_Equality",                              // System_String__op_Equality
                "op_Inequality",                            // System_String__op_Inequality
                "get_Length",                               // System_String__Length
                "get_Chars",                                // System_String__Chars
                "Format",                                   // System_String__Format
                "Combine",                                  // System_Delegate__Combine
                "Remove",                                   // System_Delegate__Remove
                "op_Equality",                              // System_Delegate__op_Equality
                "op_Inequality",                            // System_Delegate__op_Inequality
                "Zero",                                     // System_Decimal__Zero
                "MinusOne",                                 // System_Decimal__MinusOne
                "One",                                      // System_Decimal__One
                ".ctor",                                    // System_Decimal__CtorInt32
                ".ctor",                                    // System_Decimal__CtorUInt32
                ".ctor",                                    // System_Decimal__CtorInt64
                ".ctor",                                    // System_Decimal__CtorUInt64
                ".ctor",                                    // System_Decimal__CtorSingle
                ".ctor",                                    // System_Decimal__CtorDouble
                ".ctor",                                    // System_Decimal__CtorInt32Int32Int32BooleanByte
                "op_Addition",                              // System_Decimal__op_Addition
                "op_Subtraction",                           // System_Decimal__op_Subtraction
                "op_Multiply",                              // System_Decimal__op_Multiply
                "op_Division",                              // System_Decimal__op_Division
                "op_Modulus",                               // System_Decimal__op_Modulus
                "op_UnaryNegation",                         // System_Decimal__op_UnaryNegation
                "op_Increment",                             // System_Decimal__op_Increment
                "op_Decrement",                             // System_Decimal__op_Decrement
                "Negate",                                   // System_Decimal__NegateDecimal
                "Remainder",                                // System_Decimal__RemainderDecimalDecimal
                "Add",                                      // System_Decimal__AddDecimalDecimal
                "Subtract",                                 // System_Decimal__SubtractDecimalDecimal
                "Multiply",                                 // System_Decimal__MultiplyDecimalDecimal
                "Divide",                                   // System_Decimal__DivideDecimalDecimal
                "Remainder",                                // System_Decimal__ModuloDecimalDecimal
                "Compare",                                  // System_Decimal__CompareDecimalDecimal
                "op_Equality",                              // System_Decimal__op_Equality
                "op_Inequality",                            // System_Decimal__op_Inequality
                "op_GreaterThan",                           // System_Decimal__op_GreaterThan
                "op_GreaterThanOrEqual",                    // System_Decimal__op_GreaterThanOrEqual
                "op_LessThan",                              // System_Decimal__op_LessThan
                "op_LessThanOrEqual",                       // System_Decimal__op_LessThanOrEqual
                "op_Implicit",                              // System_Decimal__op_Implicit_FromByte
                "op_Implicit",                              // System_Decimal__op_Implicit_FromChar
                "op_Implicit",                              // System_Decimal__op_Implicit_FromInt16
                "op_Implicit",                              // System_Decimal__op_Implicit_FromInt32
                "op_Implicit",                              // System_Decimal__op_Implicit_FromInt64
                "op_Implicit",                              // System_Decimal__op_Implicit_FromSByte
                "op_Implicit",                              // System_Decimal__op_Implicit_FromUInt16
                "op_Implicit",                              // System_Decimal__op_Implicit_FromUInt32
                "op_Implicit",                              // System_Decimal__op_Implicit_FromUInt64
                "op_Explicit",                              // System_Decimal__op_Explicit_ToByte
                "op_Explicit",                              // System_Decimal__op_Explicit_ToUInt16
                "op_Explicit",                              // System_Decimal__op_Explicit_ToSByte
                "op_Explicit",                              // System_Decimal__op_Explicit_ToInt16
                "op_Explicit",                              // System_Decimal__op_Explicit_ToSingle
                "op_Explicit",                              // System_Decimal__op_Explicit_ToDouble
                "op_Explicit",                              // System_Decimal__op_Explicit_ToChar
                "op_Explicit",                              // System_Decimal__op_Explicit_ToUInt64
                "op_Explicit",                              // System_Decimal__op_Explicit_ToInt32
                "op_Explicit",                              // System_Decimal__op_Explicit_ToUInt32
                "op_Explicit",                              // System_Decimal__op_Explicit_ToInt64
                "op_Explicit",                              // System_Decimal__op_Explicit_FromDouble
                "op_Explicit",                              // System_Decimal__op_Explicit_FromSingle
                "MinValue",                                 // System_DateTime__MinValue
                ".ctor",                                    // System_DateTime__CtorInt64
                "Compare",                                  // System_DateTime__CompareDateTimeDateTime
                "op_Equality",                              // System_DateTime__op_Equality
                "op_Inequality",                            // System_DateTime__op_Inequality
                "op_GreaterThan",                           // System_DateTime__op_GreaterThan
                "op_GreaterThanOrEqual",                    // System_DateTime__op_GreaterThanOrEqual
                "op_LessThan",                              // System_DateTime__op_LessThan
                "op_LessThanOrEqual",                       // System_DateTime__op_LessThanOrEqual
                "GetEnumerator",                            // System_Collections_IEnumerable__GetEnumerator
                "Current",                                  // System_Collections_IEnumerator__Current
                "get_Current",                              // System_Collections_IEnumerator__get_Current
                "MoveNext",                                 // System_Collections_IEnumerator__MoveNext
                "Reset",                                    // System_Collections_IEnumerator__Reset
                "GetEnumerator",                            // System_Collections_Generic_IEnumerable_T__GetEnumerator
                "Current",                                  // System_Collections_Generic_IEnumerator_T__Current
                "get_Current",                              // System_Collections_Generic_IEnumerator_T__get_Current
                "Dispose",                                  // System_IDisposable__Dispose
                "Length",                                   // System_Array__Length
                "LongLength",                               // System_Array__LongLength
                "GetLowerBound",                            // System_Array__GetLowerBound
                "GetUpperBound",                            // System_Array__GetUpperBound
                "GetHashCode",                              // System_Object__GetHashCode
                "Equals",                                   // System_Object__Equals
                "ToString",                                 // System_Object__ToString
                "ReferenceEquals",                          // System_Object__ReferenceEquals
                "op_Explicit",                              // System_IntPtr__op_Explicit_ToPointer
                "op_Explicit",                              // System_IntPtr__op_Explicit_ToInt32
                "op_Explicit",                              // System_IntPtr__op_Explicit_ToInt64
                "op_Explicit",                              // System_IntPtr__op_Explicit_FromPointer
                "op_Explicit",                              // System_IntPtr__op_Explicit_FromInt32
                "op_Explicit",                              // System_IntPtr__op_Explicit_FromInt64
                "op_Explicit",                              // System_UIntPtr__op_Explicit_ToPointer
                "op_Explicit",                              // System_UIntPtr__op_Explicit_ToUInt32
                "op_Explicit",                              // System_UIntPtr__op_Explicit_ToUInt64
                "op_Explicit",                              // System_UIntPtr__op_Explicit_FromPointer
                "op_Explicit",                              // System_UIntPtr__op_Explicit_FromUInt32
                "op_Explicit",                              // System_UIntPtr__op_Explicit_FromUInt64
                "GetValueOrDefault",                        // System_Nullable_T_GetValueOrDefault
                "get_Value",                                // System_Nullable_T_get_Value
                "get_HasValue",                             // System_Nullable_T_get_HasValue
                ".ctor",                                    // System_Nullable_T__ctor
                "op_Implicit",                              // System_Nullable_T__op_Implicit_FromT
                "op_Explicit",                              // System_Nullable_T__op_Explicit_ToT
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

        public static MemberDescriptor GetDescriptor(SpecialMember member)
        {
            return s_descriptors[(int)member];
        }
    }
}
