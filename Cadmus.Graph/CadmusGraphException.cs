using System;
using System.Runtime.Serialization;

namespace Cadmus.Graph
{
    /// <summary>
    /// An exception specific to Cadmus graph handling.
    /// </summary>
    [Serializable]
    public class CadmusGraphException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CadmusGraphException()
        {
        }

        /// <summary>
        /// Create a new exception with the specified error message.
        /// </summary>
        /// <param name="message">error message</param>
        public CadmusGraphException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create a new exception with the specified error message and inner
        /// exception.
        /// </summary>
        /// <param name="message">error message</param>
        /// <param name="innerException">inner exception</param>
        public CadmusGraphException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChironException"/> class.
        /// </summary>
        /// <param name="serializationInfo">The <see cref="SerializationInfo" /> that holds
        /// the serialized object data about the exception being thrown.</param>
        /// <param name="streamingContext">The <see cref="StreamingContext" /> that
        /// contains contextual information about the source or destination.</param>
        protected CadmusGraphException(SerializationInfo serializationInfo,
            StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
        }
    }
}
