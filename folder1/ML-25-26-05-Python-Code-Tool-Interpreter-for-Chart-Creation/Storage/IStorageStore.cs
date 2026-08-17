using System;
using System.Collections.Generic;
using System.Text;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage
{
    internal interface IStorageStore
    {
        /// <summary>
        /// Asynchronously uploads a file from the source path into the storage store under a standard reference name.
        /// </summary>
        /// <param name="sourcePath">The local filesystem path of the original file.</param>
        /// <param name="referenceName">The new reference name for the stored file.</param>
        /// <returns>A task representing the asynchronous upload operation.</returns>
        Task UploadAsync(string sourcePath, string referenceName);

        /// <summary>
        /// Lists available stored files matching an optional filter.
        /// </summary>
        /// <param name="nameFilter">The optional substring to filter file names by.</param>
        /// <returns>An enumerable collection of matching file names.</returns>
        IEnumerable<string> List(string? nameFilter = null);

        /// <summary>
        /// Deletes stored files matching an optional filter.
        /// </summary>
        /// <param name="nameFilter">The optional substring to filter file names by.</param>
        /// <returns>The number of successfully deleted files.</returns>
        int Delete(string? nameFilter = null);

        /// <summary>
        /// Asynchronously reads the text content of a stored file, up to a specified maximum number of characters.
        /// </summary>
        /// <param name="referenceName">The reference name of the stored file.</param>
        /// <param name="maxChars">The maximum number of characters to read.</param>
        /// <returns>The textual content of the file.</returns>
        Task<string> ReadTextAsync(string referenceName, int maxChars = 4000);

        /// <summary>
        /// Resolves the absolute system path for a given stored file reference name.
        /// </summary>
        /// <param name="referenceName">The reference name of the file to resolve.</param>
        /// <returns>The absolute path to the file in the underlying storage.</returns>
        string GetAbsolutePath(string referenceName);
    }
}

