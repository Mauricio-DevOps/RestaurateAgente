using Restaurantes.Web.Models;

namespace Restaurantes.Web.Services;

public static class QueueRules
{
    public static int GetOperationalStatusRank(OperationalEventStatus status)
    {
        return status switch
        {
            OperationalEventStatus.PENDENTE => 0,
            OperationalEventStatus.EM_ATENDIMENTO => 1,
            OperationalEventStatus.RESOLVIDO => 2,
            _ => 3
        };
    }

    public static string BuildOwnershipLabel(Guid? selectedWaiterId, Guid? assignedWaiterId, string? assignedWaiterName)
    {
        if (selectedWaiterId.HasValue && assignedWaiterId == selectedWaiterId)
        {
            return "Sua mesa";
        }

        if (assignedWaiterId.HasValue)
        {
            return $"Mesa de {assignedWaiterName ?? "outro garçom"}";
        }

        return "Fila geral";
    }
}
