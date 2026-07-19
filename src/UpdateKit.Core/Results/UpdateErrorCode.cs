namespace UpdateKit;

public enum UpdateErrorCode
{
    Unknown = 0,
    InvalidConfiguration,
    AuthenticationFailed,
    RepositoryNotFound,
    RateLimitExceeded,
    NetworkError,
    MalformedResponse,
    NoReleaseFound,
    InvalidVersion,
    AssetNotFound,
    DownloadFailed,
    DownloadCanceled,
    FileSystemError,
    ChecksumNotFound,
    InvalidChecksum,
    ChecksumMismatch,
}
