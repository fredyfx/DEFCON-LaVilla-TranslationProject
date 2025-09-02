using defconflix.Interfaces;

namespace defconflix.Endpoints
{
    public class Public : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/", () =>
                Results.Text("""
                GitHub OAuth API with JWT and Rate Limiting
    
                Authentication Endpoints:
                - GET /login - Start GitHub OAuth flow (WIP)
                - GET /profile - View profile, API key and JWT token (requires GitHub OAuth)
                - POST /api/reset-key - Reset API key (requires GitHub OAuth)
                - POST /api/auth/token - Get JWT token using API key
                - GET /logout - Logout
    
                Protected Endpoints (require Bearer token or X-API-Key header):
                - GET /api/protected/user-info - Get user information
                - GET /api/protected/user-stats - Get detailed user statistics
                - POST /api/vttfile - Initialize the processing of files
                - POST /api/vttcue - Updating the database along your processing on-demand

                Admin Endpoints (require JWT token):
                - GET /api/admin/users - List all users

                Public Access:
                - GET /api/users
                - GET /api/files/{type} - mp4, pdf, srt, txt are the current options available.
                - GET /api/files/{type}?page=5&pagesize=20 - pagination available.
                - GET /api/file/{id} - this provides the detail of a file.
                - GET /api/files/download?ids=1,2,3... - This handles up to 20 Ids. It downloads a text file.                
                - POST /api/files/download - This expects to have in the body request: Ids[] where you can enter more than 20.
                - GET /api/tools/downloader - This provides a script to download the videos inside of a text file.               
    
                Rate Limits:
                - Auth endpoints: 10 requests/minute
                - API Key endpoints: 100 requests/minute  
                - Authenticated endpoints: 500 requests/minute (token bucket)
                - Global: 20 requests/minute
    
                Authentication Methods:
                1. Header: Authorization: Bearer <jwt-token>
                2. Header: X-API-Key: <your-guid-here>
                """, "text/plain"));
        }
    }
}
