// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace CSharp.Meta
{
    public static class MetaExtensions
    {
        /// <summary>
        /// Portable implementation of System.Type.IsAssignableFrom(System.Type) to be used for introducing type safety
        /// assertions in decorator method code
        /// </summary>
        /// <param name="targetType">The target type of the typecast</param>
        /// <param name="sourceType">The source type of the typecast</param>
        /// <returns>True if values of type <paramref name="sourceType"/> can always be assigned to a variable of type
        /// <paramref name="targetType"/></returns>
        /// <remarks>
        /// Since the portable .NET Framework library does not support the method System.Type.IsAssignableFrom(System.Type) out of the box,
        /// this method serves as a workaround.
        /// </remarks>
        public static bool IsAssignableFrom(Type targetType, Type sourceType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            return targetType.GetTypeInfo().IsAssignableFrom(sourceType.GetTypeInfo());
        }
    }
}
