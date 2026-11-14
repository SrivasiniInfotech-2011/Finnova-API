namespace Finnova.Models.Domain.Enums;

public enum AccountRequestStatus { Pending = 0, Processing = 1, Retry = 2, Completed = 3, DeadLetter = 4 }

public enum KycStatus { Pending = 0, Passed = 1, Failed = 2, PartiallyVerified = 3, Unavailable = 4, Bypassed = 5 }

public enum KycCheckResult { Passed = 0, Failed = 1, NotChecked = 2, Unavailable = 3, Bypassed = 4 }

public enum KycSource { Bypass = 0, Internal = 1, Aggregator = 2, Manual = 3 }

public enum KycMode { Bypass = 0, Internal = 1, Aggregator = 2 }

public enum KycResolutionStatus { Pending = 0, Approved = 1, Rejected = 2 }