namespace Cadmus.Graph
{
    /// <summary>
    /// UID builder interface. An UID builder takes a SID and an unsuffixed
    /// UID, and returns an eventually suffixed UID, granted to be unique in
    /// its data space. So, this ensures that each SID + generated UID always
    /// correspond to the same UID, and no generated UIDs collide.
    /// </summary>
    public interface IUidBuilder
    {
        /// <summary>
        /// Build the eventually suffixed UID.
        /// </summary>
        /// <param name="sid">The source ID (SID).</param>
        /// <param name="unsuffixed">The generated, unsuffixed UID.</param>
        /// <returns>UID, eventually suffixed with #N.</returns>
        string Build(string sid, string unsuffixed);
    }
}
