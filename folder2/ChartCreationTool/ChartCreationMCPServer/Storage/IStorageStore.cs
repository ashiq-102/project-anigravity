using System;
using System.Collections.Generic;
using System.Text;

namespace ChartCreationMCPServer.Storage
{
    public interface IStorageStore
    {

        /// <summary>
        /// Uploads a file from the source path into the team's area of the storage store.
        /// The reference name is derived from the original filename (without extension).
        /// If a file with the same name already exists, it is overwritten.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="team"></param>
        /// <returns>The reference name derived from the uploaded filename</returns>
        Task<string> UploadAsync(string sourcePath, string team);

        /// <summary>
        /// Lists available stored files matching an optional filter.
        /// </summary>
        /// <param name="team"></param>
        /// <param name="nameFilter"></param>
        /// <returns>An enumerable collection of matching file names.</returns>
        IEnumerable<string> List(string team, string? nameFilter = null);

        /// <summary>
        /// Deletes stored files matching an optional filter.
        /// </summary>
        /// <param name="nameFilter">The optional substring to filter file names by.</param>
        /// <returns>The number of successfully deleted files.</returns>
        int Delete(string team, string? nameFilter = null);

        /// <summary>
        /// Asynchronously reads the text content of a stored file, up to a specified maximum number of characters.
        /// </summary>
        /// <param name="referenceName">The reference name of the stored file (e.g. "sales").</param>
        /// <param name="maxChars">The maximum number of characters to read.</param>
        /// <returns>The textual content of the file.</returns>
        Task<string> ReadTextAsync(string referenceName, string team, int maxChars = 4000);

        /// <summary>
        /// Resolves the absolute local path for a given stored file reference name.
        /// For Azure Blob Storage this downloads the blob to a local temp file if not already cached,
        /// and re-downloads it if the blob has been updated since it was last cached.
        /// </summary>
        /// <param name="referenceName">The reference name of the file to resolve (e.g. "sales").</param>
        /// <returns>The absolute local temp path to that file so Python can read it.</returns>
        string GetAbsolutePath(string referenceName, string team);

        /// <summary>
        /// Uploads a generated chart PNG to the output store and returns a publicly accessible URL.
        /// For Azure Blob Storage this uploads to the output container and returns a time-limited SAS URL.
        /// </summary>
        /// <param name="localImagePath">The local path of the generated PNG file.</param>
        /// <param name="chartId">The chart identifier used to name the blob.</param>
        /// <returns>A publicly accessible SAS URL for viewing/downloading the chart.</returns>
        Task<string> UploadChartAsync(string localImagePath, string chartId, string team);

        /// <summary>
        /// Lists every chart in the team's output area, each with a freshly generated SAS URL.
        /// SAS URLs cannot be stored (they expire), so a new one is minted per listing.
        /// </summary>
        /// <param name="team">The team whose charts to list.</param>
        /// <returns>Name and viewable URL for each chart.</returns>
        IEnumerable<(string Name, string Url)> ListCharts(string team);

        /// <summary>
        /// Deletes local temp cache files that are older than the specified number of hours.
        /// Azure Blob Storage is never touched by this operation — only the local server temp folder.
        /// If a user requests a file that was cleaned from cache, it will be re-downloaded from blob automatically.
        /// </summary>
        /// <param name="olderThanHours">Files older than this many hours will be deleted from the local temp cache.</param>
        /// <returns>The number of local temp files deleted.</returns>
        Task<int> CleanupTempCacheAsync(int olderThanHours = 24);
    }
}
