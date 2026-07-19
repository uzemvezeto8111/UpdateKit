namespace UpdateKit;

/// <summary>Identifies stable categories of update-operation failures.</summary>
public enum UpdateErrorCode
{
    /// <summary>An unexpected failure without a more specific category.</summary>
    Unknown = 0,
    /// <summary>Caller-supplied configuration is invalid.</summary>
    InvalidConfiguration,
    /// <summary>GitHub rejected credentials or repository permissions.</summary>
    AuthenticationFailed,
    /// <summary>The repository was not found or is not accessible.</summary>
    RepositoryNotFound,
    /// <summary>The GitHub API rate limit was exceeded.</summary>
    RateLimitExceeded,
    /// <summary>An HTTP request or other network operation failed.</summary>
    NetworkError,
    /// <summary>A remote response could not be parsed or validated.</summary>
    MalformedResponse,
    /// <summary>No eligible published release was found.</summary>
    NoReleaseFound,
    /// <summary>A current version or eligible release tag is invalid.</summary>
    InvalidVersion,
    /// <summary>No release asset matched the selection criteria.</summary>
    AssetNotFound,
    /// <summary>An asset transfer failed.</summary>
    DownloadFailed,
    /// <summary>A download or verification operation was canceled by the caller.</summary>
    DownloadCanceled,
    /// <summary>A local file-system operation failed.</summary>
    FileSystemError,
    /// <summary>No matching checksum-file entry was found.</summary>
    ChecksumNotFound,
    /// <summary>A checksum value or checksum-file entry is invalid or ambiguous.</summary>
    InvalidChecksum,
    /// <summary>The downloaded content does not match the expected checksum.</summary>
    ChecksumMismatch,
}
