using Safar.Domain.Common;

namespace Safar.Domain.Entities;

public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>1-50 % off the subtotal.</summary>
    public int DiscountPercent { get; set; }

    /// <summary>Optional cap on the discount in QAR.</summary>
    public decimal? MaxDiscount { get; set; }

    public DateTime ValidUntil { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;

    public string? WhyInvalid(DateTime utcNow) =>
        !IsActive ? "This promo code is no longer active."
        : utcNow > ValidUntil ? "This promo code has expired."
        : MaxUses.HasValue && UsedCount >= MaxUses ? "This promo code has reached its usage limit."
        : null;

    public void Redeem(DateTime utcNow)
    {
        var problem = WhyInvalid(utcNow);
        if (problem is not null) throw new DomainException(problem);
        UsedCount++;
    }
}
