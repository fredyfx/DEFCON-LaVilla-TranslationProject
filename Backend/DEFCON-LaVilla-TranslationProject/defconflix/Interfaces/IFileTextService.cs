using defconflix.Models;

namespace defconflix.Interfaces
{
    public interface IFileTextService
    {
        Task<string?> GetPureTextAsync(long vttFileId, VttTextExtractionOptions? options = null);
        Task<string?> GetPureTextByIdAsync(long id, VttTextExtractionOptions? options = null);
    }
}
