using defconflix.Interfaces;

namespace defconflix.Endpoints
{
    public class Public : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/", () =>
                Results.Text("""
                Command Center - DEFCON La Villa Hacker - Translation Project!
                ======================================= 
                
                Welcome to the Command Center of a project from La Villa Hacker!
                This platform is designed to help you process, translate, and analyze multimedia files 
                using AI technologies.

                This project aims to:
                ---------------------------------------                                
                Be able to search inside of the videos and PDFs.
                Be able to share great content with our friends that don't speak English by providing them a good searching tool for video content and get the subtitles in a target language.

                We know that YouTube and other platforms are very restrictive with their content.
                The FTP Media Server of DEFCON and INFOCON already have great content.
                
                
                In a nutshell, What if...
                ---------------------------------------                                
                Videos -> Transcribe (TXT and VTT File Generated) -> Translate (another TXT and VTT File Generated) -> Summarize -> Searchable Text
                PDFs -> Convert to Markdown -> Translate -> Summarize -> Searchable Text
                

                Roadmap:
                ---------------------------------------
                Phase 0 : Develop the initial proof of concept - Completed. 
                Phase 1 : Develop the API - Completed (seems stable).
                Phase 2 : Develop the Client for processing mp4 files - In Progress.
                Phase 3 : Develop the Client for VTT Files translation - On Queue.
                Phase 4 : Develop the Client for processing PDF Files to markdown and translate them - On Queue.
                Phase 5 : Develop the Summarization Engine for text files (VTT and PDFs in markdown) - On Queue.
                Phase 6 : Develop the Search Engine with Vectorized Embeddings - On Queue. Help required.
                Phase 7 : Integrate with LLMs - On Queue. Help required.

    
                Authentication Endpoints:
                ---------------------------------------
                - GET  /login - Start GitHub OAuth flow
                - GET  /profile - View profile, API key and JWT token (requires GitHub OAuth)
                - GET  /logout - Logout
                - POST /api/reset-key - Reset API key (requires GitHub OAuth)
                - POST /api/auth/token - Get JWT token using API key

                * Note to myself or to any potential helper:
                - RefreshToken is not implemented, yet.
    
                Protected Endpoints (require Bearer token or X-API-Key header):
                ---------------------------------------
                - GET  /api/protected/user-info - Get user information
                - GET  /api/protected/user-stats - Get detailed user statistics

                Endpoints for processing files (require X-API-Key header):
                - POST /api/vttfile/start - Initialize the processing of files
                - POST /api/vttfile/completed/{id} - Finalize the processing of files
                - GET  /api/vttfile/export/{id} - Export the processed file (pure TXT for now)
                - POST /api/vttcue - Updating the database along your processing on-demand

                Endpoints that requires X-API-Key:
                ---------------------------------------
                - GET  /api/file/{id} - this provides the detail of a file.
                - GET  /api/file/search/{filename} - search by filename (exact match).
                - GET  /api/files/{type} - List by mp4, pdf, srt, txt are the current options available.
                - GET  /api/files/{type}?page=5&pagesize=20 - pagination available.
                - GET  /api/files/{type}/search/{term} - search inside the text files (case insensitive).
                - GET  /api/files/download?ids=1,2,3... - This handles up to 20 Ids. It downloads a text file.                
                - POST /api/files/download - This expects to have in the body request: Ids[] where you can enter more than 20.
                - GET  /api/admin/dashboard - Admin dashboard with stats. (Just an idea for now)
                - GET  /api/users - List all users with pagination.

                Public Access:
                ---------------------------------------
                - GET /api/tools/downloader - This provides a script to download the videos inside of a text file.               
    
                Endpoints that requires Admin Role:
                ---------------------------------------
                - GET  /api/background/jobs/{jobId}/status - Get job status.
                - GET  /api/background/jobs/active - Get all active jobs.
                - GET  /api/background/jobs/queue/status - Get queue status.
                - POST /api/background/jobs/{jobId}/cancel - Cancel a job.
                - POST /api/background/jobs/check-files - Start a file check job for specific files
                - POST /api/background/jobs/check-all - Start a job to check all files.
                - POST /api/background/jobs/check-needed - Start a job to check files that need checking (not checked in 24h).

                Rate Limits:
                ---------------------------------------
                - Auth endpoints: 10 requests/minute
                - API Key endpoints: 100 requests/minute  
                - Authenticated endpoints: 500 requests/minute (token bucket)
                - Global: 20 requests/minute
    
                Authentication Methods:
                ---------------------------------------
                1. Header: Authorization: Bearer <jwt-token>
                2. Header: X-API-Key: <your-guid-here>
                """, "text/plain"));
        }
    }
}
