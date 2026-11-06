namespace Finnova.Models.Domain.Enums;

public enum JvPostingStatus
{
    Ready,
    Processing,
    RetryPending,
    Posted,
    DeadLetter,
    Cancelled
}
