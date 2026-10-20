
using OrderProcessing.Repositories;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Services;

public class DiscountService
{
    private readonly CouponRepository _couponRepository;

    public DiscountService(CouponRepository couponRepository)
    {
        _couponRepository = couponRepository;
    }

    public async Task<decimal> GetDiscountAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        Log.Info($"DiscountService -> Order #{order.Id}: DNS/handshake...");
        await Task.Delay(150, ct);

        Log.Info($"DiscountService -> Order #{order.Id}: Sending discount request...");
        await Task.Delay(300, ct);

        int loyaltyDiscount = customer.LoyaltyTier switch
        {
            LoyaltyTier.Bronze => 0,
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };

        int couponDiscount = 0;

        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            Coupon? coupon = await _couponRepository.GetByCodeAsync(
                order.CouponCode,
                ct);

            if (coupon is null)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' has expired");
            }

            couponDiscount = coupon.DiscountPercent;
        }

        Log.Info($"DiscountService -> Order #{order.Id}: Waiting for response...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        ct.ThrowIfCancellationRequested();

        int totalDiscountPercent = Math.Min(
            loyaltyDiscount + couponDiscount,
            30);

        decimal discount = Math.Round(
            order.SubTotal * totalDiscountPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"DiscountService -> Order #{order.Id}: " +
            $"Loyalty {loyaltyDiscount}% + Coupon {couponDiscount}% " +
            $"= {totalDiscountPercent}% (Discount = {discount:F2})");

        return discount;
    }
}
