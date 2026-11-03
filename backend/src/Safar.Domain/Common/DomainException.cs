namespace Safar.Domain.Common;

/// <summary>A business rule was broken (e.g. not enough seats, booking already cancelled).</summary>
public class DomainException(string message) : Exception(message);
