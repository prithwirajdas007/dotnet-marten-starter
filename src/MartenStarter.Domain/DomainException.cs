namespace MartenStarter.Domain;

// Thrown when a business rule would be broken — e.g. trying to execute an unpriced trade.
// Distinct from ArgumentException (bad input) so endpoints can map it to 422 Unprocessable Entity
// while leaving argument exceptions as 400 Bad Request.
public sealed class DomainException(string message) : Exception(message);
