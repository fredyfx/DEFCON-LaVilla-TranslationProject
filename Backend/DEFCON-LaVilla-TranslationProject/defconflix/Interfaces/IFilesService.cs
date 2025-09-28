namespace defconflix.WebAPI.Interfaces
{
    public interface IFilesService
    {
        Task<(IEnumerable<FileDTO> Files, PaginationInfo Pagination)> GetFilesByTypeAsync(
            string fileType,
            int page = 1,
            int pageSize = 10);

        Task<(IEnumerable<FileDTO> Files, PaginationInfo Pagination)> SearchFilesByTypeConferenceAndTerm(
            string fileTypeRequested, 
            string conference, 
            string term, 
            int page = 1, 
            int pageSize = 10);

        Task<FileDTO> GetFileDetailsByExactName(string fileName);
        Task<string?> GetFileLocationAsync(long id);

        Task<byte[]> GenerateBulkDownloadAsync(long[] ids);

        Task<byte[]> GenerateDownloadFromQueryAsync(string idsQuery);
    }

    public record FileDTO(long Id, string FileName, string Conference, string Status);

    public record PaginationInfo(
        int CurrentPage,
        int PageSize,
        int TotalPages,
        int TotalFiles,
        bool HasPreviousPage,
        bool HasNextPage);
}
