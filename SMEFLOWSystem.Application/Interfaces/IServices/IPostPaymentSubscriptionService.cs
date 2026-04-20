using SMEFLOWSystem.Application.Events.Payments;

namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IPostPaymentSubscriptionService
{
    Task HandlePaymentSucceededAsync(PaymentSucceededEvent message, CancellationToken cancellationToken = default);
}
