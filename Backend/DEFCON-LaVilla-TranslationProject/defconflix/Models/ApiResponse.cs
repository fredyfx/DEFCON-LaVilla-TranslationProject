namespace defconflix.Models
{
    /// <summary>
    /// Standardized API response wrapper for consistent responses across all endpoints.
    /// </summary>
    /// <typeparam name="T">The type of data being returned</typeparam>
    public record ApiResponse<T>
    {
        public bool Success { get; init; } = true;
        public T? Data { get; init; }
        public string? Message { get; init; }
        public string? CorrelationId { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public static ApiResponse<T> Ok(T data, string? message = null) => new()
        {
            Success = true,
            Data = data,
            Message = message
        };

        public static ApiResponse<T> Error(string message) => new()
        {
            Success = false,
            Message = message
        };
    }

    /// <summary>
    /// Paginated API response wrapper
    /// </summary>
    public record PaginatedResponse<T>
    {
        public bool Success { get; init; } = true;
        public IEnumerable<T> Data { get; init; } = Enumerable.Empty<T>();
        public WebAPI.Interfaces.PaginationInfo Pagination { get; init; } = default!;
        public string? Message { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public static PaginatedResponse<T> Ok(IEnumerable<T> data, WebAPI.Interfaces.PaginationInfo pagination) => new()
        {
            Success = true,
            Data = data,
            Pagination = pagination
        };
    }

    /// <summary>
    /// Job status response
    /// </summary>
    public record JobStatusResponse
    {
        public string JobId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime StartedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public int TotalItems { get; init; }
        public int ProcessedItems { get; init; }
        public double ProgressPercentage { get; init; }
        public string? Duration { get; init; }
        public bool IsCompleted { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
