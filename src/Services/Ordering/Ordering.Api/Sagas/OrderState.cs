using MassTransit;

namespace Ordering.Api.Sagas;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public DateTime ReservationDeadline { get; set; }
    public bool PaymentAccepted { get; set; }
}
