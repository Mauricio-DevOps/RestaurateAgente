using Restaurantes.Web.Services;

namespace Restaurantes.Tests;

public sealed class RestaurantTextTests
{
    [Fact]
    public void Slugify_NormalizesRestaurantNames()
    {
        Assert.Equal("bistro-da-praca", RestaurantText.Slugify("Bistrô da Praça"));
    }

    [Fact]
    public void Slugify_FallsBackWhenNameHasNoValidCharacters()
    {
        Assert.Equal("restaurante", RestaurantText.Slugify("!!!"));
    }

    [Fact]
    public void NormalizeWaiterNameAndTableNumber_TrimAndCollapseWhitespace()
    {
        Assert.Equal("Ana Maria", RestaurantText.NormalizeWaiterName("  Ana   Maria  "));
        Assert.Equal("Mesa 12", RestaurantText.NormalizeTableNumber(" Mesa   12 "));
    }

    [Fact]
    public void DuplicateValidation_RejectsDuplicateWaitersAndTables()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RestaurantText.EnsureNoDuplicateWaiters(["Ana Maria", " ana   maria "]));
        Assert.Throws<InvalidOperationException>(() =>
            RestaurantText.EnsureNoDuplicateTables(["1", " 1 "]));
    }
}
