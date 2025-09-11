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
                
                // Banner intro
                await TypeText(context, GenerateTypingBanner(), 50);
                await Task.Delay(1000, context.RequestAborted);

                // DEFCON La Villa Hacker
                await context.Response.WriteAsync("\x1b[2J\x1b[H");
                await context.Response.Body.FlushAsync();
                await TypeText(context, GenerateAnimatedFrames(), 20);
                await Task.Delay(1500, context.RequestAborted);

                // Footer
                await TypeText(context, GenerateAsciiArtFooter(), 30);
                await Task.Delay(1000, context.RequestAborted);

                // End
                await TypeText(context, "\n\n            [TRANSMISSION COMPLETE]", 100);
            });
        }

        private static string GenerateAsciiArtFooter()
        {
            // Simple ASCII art generator - you can replace with more sophisticated libraries
            var result = new StringBuilder();
            result.AppendLine();
            result.AppendLine("            ╔══════════════════════════════════════════════════════════════╗");
            result.AppendLine("                           🔥 DEFCON La Villa Hacker 🔥                    ");
            result.AppendLine("            ╠══════════════════════════════════════════════════════════════╣");
            result.AppendLine("                               ...What are you looking for?                 ");
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
                ░            🏴‍☠️ Hackers Hispanos! 🏴‍☠️          ░
                ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
                
                ";
        }

        private static string GenerateTypingBanner()
        {
            return @"
            ╔════════════════════════════════════════════════════════════════╗
                                   🔐 SECURE TERMINAL 🔐                    
                                  Connection Established                     
            ╚════════════════════════════════════════════════════════════════╝
    
            > Initializing transmission...
            > ";
        }

        private static async Task TypeText(HttpContext context, string text, int delayMs)
        {
            foreach (char c in text)
            {
                if (context.RequestAborted.IsCancellationRequested)
                    break;

                await context.Response.WriteAsync(c.ToString());
                await context.Response.Body.FlushAsync();
                await Task.Delay(delayMs, context.RequestAborted);
            }
        }
    }
}
