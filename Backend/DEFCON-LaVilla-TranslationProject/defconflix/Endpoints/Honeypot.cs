using defconflix.Interfaces;
using System.Text;

namespace defconflix.Endpoints
{
    public class Honeypot : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/index.php", async (HttpContext context, string? text = "LAVILLA") =>
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.Headers.Add("Transfer-Encoding", "chunked");

                var banner = GenerateTypingBanner();
                var linesInBanner = banner.Split('\n');

                foreach (var line in linesInBanner)
                {
                    if (context.RequestAborted.IsCancellationRequested)
                        break;

                    await context.Response.WriteAsync(line + "\n");
                    await context.Response.Body.FlushAsync();
                    await Task.Delay(500, context.RequestAborted);
                }

                var frames = GenerateAnimatedFrames();
                var linesInFrames = frames.Split('\n');
                await context.Response.WriteAsync("\x1b[2J\x1b[H");
                foreach (var line in linesInFrames)
                {
                    if (context.RequestAborted.IsCancellationRequested)
                        break;

                    await context.Response.WriteAsync(line + "\n");
                    await context.Response.Body.FlushAsync();
                    await Task.Delay(500, context.RequestAborted);
                }
                var footer = GenerateAsciiArtFooter();
                var linesInFooter = footer.Split('\n');
                foreach (var line in linesInFooter)
                {
                    if (context.RequestAborted.IsCancellationRequested)
                        break;

                    await context.Response.WriteAsync(line + "\n");
                    await context.Response.Body.FlushAsync();
                    await Task.Delay(500, context.RequestAborted);
                }
                await context.Response.WriteAsync("\n\n            [TRANSMISSION COMPLETE]");
                await context.Response.Body.FlushAsync();
            });
        }

        private static string GenerateAsciiArtFooter()
        {
            // Simple ASCII art generator - you can replace with more sophisticated libraries
            var result = new StringBuilder();

            result.AppendLine("            ╔══════════════════════════════════════════════════════════════╗");
            result.AppendLine("            ║               🔥 DEFCON La Villa Hacker 🔥                  ║");
            result.AppendLine("            ╠══════════════════════════════════════════════════════════════╣");
            result.AppendLine("            ╠                   ...What are you looking for?               ╣");
            result.AppendLine("            ╚══════════════════════════════════════════════════════════════╝");
            result.AppendLine($"            Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            return result.ToString();
        }


        private static string GenerateAnimatedFrames()
        {
            return
                @"
                ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
                ░  ██████  ███████ ███████  ██████  ██████  ███    ██ ░
                ░  ██   ██ ██      ██      ██      ██    ██ ████   ██ ░
                ░  ██   ██ █████   █████   ██      ██    ██ ██ ██  ██ ░
                ░  ██   ██ ██      ██      ██      ██    ██ ██  ██ ██ ░
                ░  ██████  ███████ ██       ██████  ██████  ██   ████ ░
                ░                                                     ░
                ░                 La Villa Hacker                     ░
                ░            🏴‍☠️ Hackers Hispanos! 🏴‍☠️              ░
                ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
                ";
        }

        private static string GenerateTypingBanner()
        {
            return @"
            ╔════════════════════════════════════════════════════════════════╗
            ║                      🔐 SECURE TERMINAL 🔐                     ║
            ║                     Connection Established                     ║
            ╚════════════════════════════════════════════════════════════════╝
    
            > Initializing transmission...
            > ";
        }
    }
}
