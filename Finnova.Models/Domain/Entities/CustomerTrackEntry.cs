using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class CustomerTrackEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public string? LineOfBusinessCode { get; private set; }
    public string? AccountNumber { get; private set; }
    public string TrackTypeCode { get; private set; } = string.Empty;
    public DateOnly TrackDate { get; private set; }
    public string? Reason { get; private set; }
    public string? ByLogin { get; private set; }
    public DateOnly? ReleaseDate { get; private set; }
    public string? ReleasedBy { get; private set; }
    public CustomerTrackStatus Status { get; private set; } = CustomerTrackStatus.Blacklisted;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Initialize(
        string? lineOfBusinessCode, string? accountNumber, string trackTypeCode, DateOnly trackDate,
        string? reason, string? byLogin)
    {
        LineOfBusinessCode = lineOfBusinessCode;
        AccountNumber = accountNumber;
        TrackTypeCode = trackTypeCode;
        TrackDate = trackDate;
        Reason = reason;
        ByLogin = byLogin;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Release(string actorId)
    {
        Status = CustomerTrackStatus.Released;
        ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow);
        ReleasedBy = actorId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
}
