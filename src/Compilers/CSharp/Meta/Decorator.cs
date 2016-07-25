// Copyright (c) Aleksandar Dalemski.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Reflection;

namespace CSharp.Meta
{
    /// <summary>
    /// Base class for all decorators which can be applied to member declarations in order to weave aspects around them at compile time.
    /// </summary>
    public abstract class Decorator
    {
        public virtual void DecorateConstructor(ConstructorInfo constructor, object thisObject, object[] arguments)
        {
            throw new MetaException($"Cannot apply decorator class '{GetType()}' to constructor '{constructor}' because it does not support decoration of constructors.");
        }

        public virtual void DecorateDestructor(MethodInfo destructor, object thisObject)
        {
            throw new MetaException($"Cannot apply decorator class '{GetType()}' to destructor '{destructor}' because it does not support decoration of destructors.");
        }

        public virtual object DecorateIndexerGet(PropertyInfo indexer, object thisObject, object[] arguments)
        {
            throw new MetaException($"Cannot apply decorator class '{GetType()}' to indexer '{indexer}' because it does not support decoration of indexers.");
        }

        public virtual void DecorateIndexerSet(PropertyInfo indexer, object thisObject, object[] arguments, object value)
        {
            throw new MetaException($"Cannot apply decorator class '{GetType()}' to indexer '{indexer}' because it does not support decoration of indexers.");
        }

        /// <summary>
        /// When overridden in a derived class, this decorator method's body is used to weave aspects around method declarations.
        /// </summary>
        /// <param name="method">The decorated method's runtime reflection information.</param>
        /// <param name="thisObject">A reference to the decorated method's this reference, if it is an instance method.</param>
        /// <param name="arguments">A list of all arguments passed to the decorated method in its invocation.</param>
        /// <returns>The value which should be returned by the decorated method at the end of its execution.</returns>
        public virtual object DecorateMethod(MethodInfo method, object thisObject, object[] arguments)
        {
            throw new MetaException($"Cannot apply decorator class '{GetType()}' to method '{method}' because it does not support decoration of methods.");
        }

        public virtual object DecoratePropertyGet(PropertyInfo property, object thisObject)
        {
            throw new MetaException($"Cannot apply decorator class '{GetType()}' to property '{property}' because it does not support decoration of properties.");
        }

        public virtual void DecoratePropertySet(PropertyInfo property, object thisObject, object value)
        {
            throw new MetaException($"Cannot apply decorator class '{GetType()}' to property '{property}' because it does not support decoration of properties.");
        }
    }
}