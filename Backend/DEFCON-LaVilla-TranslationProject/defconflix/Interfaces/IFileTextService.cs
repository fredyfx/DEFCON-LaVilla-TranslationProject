using defconflix.Models;

namespace defconflix.Interfaces
{
    public interface IFileTextService
    {
        Task<string?> GetPureTextAsync(int vttFileId, VttTextExtractionOptions? options = null);
        Task<string?> GetPureTextByIdAsync(int id, VttTextExtractionOptions? options = null);
    }
}
