using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Tests;

public sealed class QueueRulesTests
{
    [Fact]
    public void GetOperationalStatusRank_SortsPendingBeforeInProgressAndResolved()
    {
        var statuses = new[]
        {
            OperationalEventStatus.RESOLVIDO,
            OperationalEventStatus.PENDENTE,
            OperationalEventStatus.EM_ATENDIMENTO
        };

        var sorted = statuses.OrderBy(QueueRules.GetOperationalStatusRank).ToArray();

        Assert.Equal(
            [OperationalEventStatus.PENDENTE, OperationalEventStatus.EM_ATENDIMENTO, OperationalEventStatus.RESOLVIDO],
            sorted);
    }

    [Fact]
    public void BuildOwnershipLabel_IdentifiesSelectedWaiterAndGeneralQueue()
    {
        var selected = Guid.NewGuid();
        var other = Guid.NewGuid();

        Assert.Equal("Sua mesa", QueueRules.BuildOwnershipLabel(selected, selected, "Ana"));
        Assert.Equal("Fila geral", QueueRules.BuildOwnershipLabel(selected, null, null));
        Assert.Equal("Mesa de Bruno", QueueRules.BuildOwnershipLabel(selected, other, "Bruno"));
    }
}
