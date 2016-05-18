using System;
using System.Reflection;

namespace CSharp.Meta
{
    public static class MetaPrimitives
    {
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
        /// Returns the type of the this-reference of a method, or typeof(void) if the method is static. Used to introduce this-reference subtyping assertions in decorator method type checking.
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

        public static void ApplyDecorator(MemberInfo member, Decorator decorator)
        {
            throw new InvalidOperationException("This method may only be used in metaclass code executed at compile-time.");
        }
    }
}