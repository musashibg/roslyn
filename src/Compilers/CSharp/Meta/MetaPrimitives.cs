using System;
using System.Diagnostics;
using System.Reflection;

namespace CSharp.Meta
{
    public static class MetaPrimitives
    {
        public static void AddTrait(Type hostType, Type traitType)
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compile-time.");
        }

        public static void AddTrait<T>(Type hostType)
            where T : Trait
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compile-time.");
        }

        public static void ApplyDecorator(MemberInfo member, Decorator decorator)
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compile-time.");
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
        /// Returns the type of a method's parameter. Used to introduce argument subtyping assertions in decorator method type checking.
        /// </summary>
        /// <param name="method">A method's runtime reflection information.</param>
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
        /// <param name="property">A property's runtime reflection information.</param>
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
        /// <param name="method">A method's runtime reflection information.</param>
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
        /// <param name="property">A property's runtime reflection information.</param>
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

            MethodInfo[] accessors = property.GetAccessors();
            Debug.Assert(accessors != null && accessors.Length > 0);
            return accessors[0].IsStatic ? typeof(void) : property.DeclaringType;
        }
    }
}