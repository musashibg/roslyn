using System;
using System.Runtime.Serialization;

namespace CSharp.Meta
{
    /// <summary>
    /// The exception that is thrown during the compilation of metaprogramming features.
    /// </summary>
    public class MetaException : Exception
    {
        public MetaException()
        {
        }

        public MetaException(string message)
            : base(message)
        {
        }

        public MetaException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if (!PORTABLE)
        protected MetaException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}