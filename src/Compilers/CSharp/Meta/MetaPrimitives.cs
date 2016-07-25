﻿// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Reflection;

namespace CSharp.Meta
{
    public static class MetaPrimitives
    {
        /// <summary>
        /// Imports a trait into a host type. Can only be used in a metaclass code and the host type should be the target type of the metaclass application.
        /// </summary>
        /// <param name="hostType">The host type into which the trait will be imported.</param>
        /// <param name="traitType">An arbitrary trait type.</param>
        public static void AddTrait(Type hostType, Type traitType)
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compilation time.");
        }

        /// <summary>
        /// Imports a trait into a host type. Can only be used in a metaclass code and the host type should be the target type of the metaclass application.
        /// </summary>
        /// <typeparam name="T">An arbitrary trait type.</typeparam>
        /// <param name="hostType">The host type into which the trait will be imported.</param>
        public static void AddTrait<T>(Type hostType)
            where T : Trait
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compilation time.");
        }

        /// <summary>
        /// Applies a decorator to a type member. Can only be used in a metaclass code and the member should be declared by the target type of the metaclass application.
        /// </summary>
        /// <param name="member">A member's run-time reflection information.</param>
        /// <param name="decorator">An arbitrary decorator instance.</param>
        public static void ApplyDecorator(MemberInfo member, Decorator decorator)
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compilation time.");
        }

        /// <summary>
        /// Clones an array of method arguments. The decorator method type checker considers the result of this method a proper argument array for the method.
        /// </summary>
        /// <param name="arguments">A source array of method arguments.</param>
        /// <returns>A method arguments array containing the same argument values as the source argument array.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="arguments"/> is null.
        /// </exception>
        public static object[] CloneArguments(object[] arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            int length = arguments.Length;
            var clonedArguments = new object[length];
            Array.Copy(arguments, clonedArguments, length);
            return clonedArguments;
        }

        /// <summary>
        /// Clones an array of method arguments. The decorator method type checker considers the result of this method a simple array of arbitrary values.
        /// </summary>
        /// <param name="arguments">A source array of method arguments.</param>
        /// <returns>An array containing the same elements as the source argument array.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="arguments"/> is null.
        /// </exception>
        public static object[] CloneArgumentsToObjectArray(object[] arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            int length = arguments.Length;
            var clonedArguments = new object[length];
            Array.Copy(arguments, clonedArguments, length);
            return clonedArguments;
        }

        /// <summary>
        /// Returns the default value for a type. Can be used inside decorator methods to satisfy special type safety constraints.
        /// </summary>
        /// <param name="type">An arbitrary type.</param>
        /// <returns></returns>
        public static object DefaultValue(Type type)
        {
#if (PORTABLE)
            return type.GetTypeInfo().IsValueType && type != typeof(void)
                    ? Activator.CreateInstance(type)
                    : null;
#else
            return type.IsValueType && type != typeof(void)
                    ? Activator.CreateInstance(type)
                    : null;
#endif
        }

        /// <summary>
        /// Checks if a type member is implicitly generated by the compiler. Can only be used in metaclass code.
        /// </summary>
        /// <param name="member">A member's run-time reflection information.</param>
        /// <returns>True if the member is generated implicitly by the compiler.</returns>
        public static bool IsImplicitlyDeclared(MemberInfo member)
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compilation time.");
        }

        /// <summary>
        /// Checks if a method is an iterator. Can only be used in metaclass code.
        /// </summary>
        /// <param name="method">A method's run-time reflection information.</param>
        /// <returns>True if the method is an iterator.</returns>
        public static bool IsIterator(MethodInfo method)
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compilation time.");
        }

        /// <summary>
        /// Checks if a method is a property accessor.
        /// </summary>
        /// <param name="method">A method's run-time reflection information.</param>
        /// <returns>True if the method is a property accessor.</returns>
        public static bool IsPropertyAccessor(MethodInfo method)
        {
#if (PORTABLE)
            foreach (PropertyInfo property in method.DeclaringType.GetTypeInfo().DeclaredProperties)
            {
                if (property.GetMethod == method || property.SetMethod == method)
                {
                    return true;
                }
            }
#else
            BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            if (method.IsStatic)
            {
                bindingFlags |= BindingFlags.Static;
            }
            else
            {
                bindingFlags |= BindingFlags.Instance;
            }

            PropertyInfo[] properties = method.DeclaringType.GetProperties(bindingFlags);
            foreach (PropertyInfo property in properties)
            {
                if (property.GetGetMethod() == method || property.GetSetMethod() == method)
                {
                    return true;
                }
            }
#endif
            return false;
        }

        /// <summary>
        /// Checks whether a property is read-only or not.
        /// </summary>
        /// <param name="property">A property's run-time reflection information.</param>
        /// <returns>True if the propert does not have a setter.</returns>
        public static bool IsReadOnly(PropertyInfo property)
        {
#if (PORTABLE)
            return property.SetMethod == null;
#else
            return property.GetSetMethod() == null;
#endif
        }

        /// <summary>
        /// Checks whether a property is write-only or not.
        /// </summary>
        /// <param name="property">A property's run-time reflection information.</param>
        /// <returns>True if the propert does not have a getter.</returns>
        public static bool IsWriteOnly(PropertyInfo property)
        {
#if (PORTABLE)
            return property.GetMethod == null;
#else
            return property.GetGetMethod() == null;
#endif
        }

        /// <summary>
        /// Returns the type of a method's parameter. Used to introduce argument subtyping assertions in decorator method type checking.
        /// </summary>
        /// <param name="method">A method's run-time reflection information.</param>
        /// <param name="parameterIndex">A valid parameter index for the method.</param>
        /// <returns>The specified parameter's type.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="method"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="parameterIndex"/> is outside the range of method parameters.
        /// </exception>
        public static Type ParameterType(MethodBase method, int parameterIndex)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameterIndex < 0 || parameterIndex >= parameters.Length)
            {
                throw new ArgumentOutOfRangeException("Parameter index out of range.", nameof(parameterIndex));
            }

            return parameters[parameterIndex].ParameterType;
        }

        /// <summary>
        /// Returns the type of a property's parameter. Used to introduce argument subtyping assertions in decorator method type checking.
        /// </summary>
        /// <param name="property">A property's run-time reflection information.</param>
        /// <param name="parameterIndex">A valid parameter index for the property.</param>
        /// <returns>The specified parameter's type.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="property"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="parameterIndex"/> is outside the range of property parameters.
        /// </exception>
        public static Type ParameterType(PropertyInfo property, int parameterIndex)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            ParameterInfo[] parameters = property.GetIndexParameters();
            if (parameterIndex < 0 || parameterIndex >= parameters.Length)
            {
                throw new ArgumentOutOfRangeException("Parameter index out of range.", nameof(parameterIndex));
            }

            return parameters[parameterIndex].ParameterType;
        }

        /// <summary>
        /// Returns the type of the this-reference of a method, or typeof(void) if the method is static. Used to introduce this-reference subtyping assertions in
        /// decorator method type checking.
        /// </summary>
        /// <param name="method">A method's run-time reflection information.</param>
        /// <returns>The type of the this-reference of the method, or typeof(void) if it is static.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="method"/> is null.
        /// </exception>
        public static Type ThisObjectType(MethodBase method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            return method.IsStatic ? typeof(void) : method.DeclaringType;
        }

        /// <summary>
        /// Returns the type of the this-reference of a property, or typeof(void) if the property is static. Used to introduce this-reference subtyping assertions in
        /// decorator method type checking.
        /// </summary>
        /// <param name="property">A property's run-time reflection information.</param>
        /// <returns>The type of the this-reference of the property, or typeof(void) if it is static.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="property"/> is null.
        /// </exception>
        public static Type ThisObjectType(PropertyInfo property)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

#if (PORTABLE)
            MethodInfo accessor = property.GetMethod ?? property.SetMethod;
            Debug.Assert(accessor != null);
            return accessor.IsStatic ? typeof(void) : property.DeclaringType;
#else
            MethodInfo[] accessors = property.GetAccessors();
            Debug.Assert(accessors != null && accessors.Length > 0);
            return accessors[0].IsStatic ? typeof(void) : property.DeclaringType;
#endif
        }
    }
}