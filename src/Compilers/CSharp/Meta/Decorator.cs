using System.Reflection;

namespace CSharp.Meta
{
    /// <summary>
    /// Base class for all decorators which can be applied to member declarations in order to weave aspects around them at compile time.
    /// </summary>
    public abstract class Decorator
    {
        /// <summary>
        /// When overridden in a derived class, this decorator method's body is used to weave aspects around method declarations.
        /// </summary>
        /// <param name="method">The decorated method's runtime reflection information.</param>
        /// <param name="thisObject">A reference to the decorated method's this reference, if it is an instance method.</param>
        /// <param name="arguments">A list of all arguments passed to the decorated method in its invocation.</param>
        /// <returns>The value which should be returned by the decorated method at the end of its execution.</returns>
        public virtual object DecorateMethod(MethodInfo method, object thisObject, object[] arguments)
        {
            return method.Invoke(thisObject, arguments);
        }
    }
}