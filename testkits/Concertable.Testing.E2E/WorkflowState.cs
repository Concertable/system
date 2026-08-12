namespace Concertable.Testing.E2E;

public sealed class WorkflowState
{
    public int VenueId { get; set; }
    public int OpportunityId { get; set; }
    public int ApplicationId { get; set; }
    public int? ConcertId { get; set; }
    public string? PurchasedTicketId { get; set; }
    public string? SignUpEmail { get; set; }
    public string? SignUpPassword { get; set; }
}
