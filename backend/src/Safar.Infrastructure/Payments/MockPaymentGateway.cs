using Microsoft.Extensions.Logging;
using Safar.Application.Common;

namespace Safar.Infrastructure.Payments;

/// <summary>
/// Stand-in for a real payment provider (QPay, Stripe, Checkout.com…).
/// Swap this class for a real implementation of IPaymentGateway - nothing else changes.
/// No card data ever reaches this API; a real integration would use the provider's hosted checkout.
/// </summary>
public class MockPaymentGateway(ILogger<MockPaymentGateway> logger) : IPaymentGateway
{
    public async Task<PaymentResult> ChargeAsync(decimal amount, string reference, string method, CancellationToken ct = default)
    {
        await Task.Delay(300, ct); // simulate provider latency

        var transactionId = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        logger.LogInformation("Mock charge {Amount} QAR for {Reference} via {Method}: {TransactionId}",
            amount, reference, method, transactionId);

        return new PaymentResult(true, transactionId, null);
    }
}
