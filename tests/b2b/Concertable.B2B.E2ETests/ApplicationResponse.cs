namespace Concertable.B2B.E2ETests;

public sealed record ApplicationResponse(ApplicationStatus Status);

public enum ApplicationStatus
{
    Pending,
    Rejected,
    Withdrawn,
    Accepted,
    Cancelled
}
