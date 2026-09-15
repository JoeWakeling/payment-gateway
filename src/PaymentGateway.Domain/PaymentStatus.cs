namespace PaymentGateway.Domain;

// No Rejected member: a rejected request never becomes a payment, so it is surfaced as an
// error response rather than stored as a status.
public enum PaymentStatus
{
    Authorized,
    Declined
}
