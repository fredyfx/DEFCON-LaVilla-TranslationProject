using defconflix.Interfaces;
using System.Reflection;
using System.Text;

namespace defconflix.Endpoints
{
    public class Tools : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/tools/downloader", () =>
            {
                // Get the executing assembly
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "defconflix.wwwroot.tools.LaVillaHackerDownloader.sh";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return Results.NotFound("Script file not found");
                }

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                var bytes = Encoding.UTF8.GetBytes(content);

                return Results.File(
                    bytes,
                    contentType: "application/x-sh",
                    fileDownloadName: "LaVillaHackerDownloader.sh"
                );
            });
        }
    }
}
