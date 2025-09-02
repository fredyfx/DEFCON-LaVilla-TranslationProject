using defconflix.Models;

namespace defconflix.Interfaces
{
    public interface IFileTextService
    {
        Task<string?> GetPureTextAsync(int vttFileId, VttTextExtractionOptions? options = null);
        Task<string?> GetPureTextByHashAsync(string hash, VttTextExtractionOptions? options = null);
    }
}
