
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Repositories;

public class CouponRepository
{
    private readonly JsonFileStore _store;

    public CouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Coupon?> GetByCodeAsync(
        string couponCode,
        CancellationToken ct)
    {
        Log.Info(
            $"CouponRepository -> Opening connection for coupon '{couponCode}'...");

        await Task.Delay(200, ct);

        Log.Info(
            $"CouponRepository -> Executing query for coupon '{couponCode}'...");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Coupon> coupons =
            await _store.ReadAsync<Coupon>(DataFiles.Coupons, ct);

        Coupon? coupon = coupons.FirstOrDefault(
            c => string.Equals(
                c.Code,
                couponCode,
                StringComparison.OrdinalIgnoreCase));

        if (coupon is null)
        {
            Log.Error(
                $"CouponRepository -> Coupon '{couponCode}' not found.");
        }
        else
        {
            Log.Success(
                $"CouponRepository -> Coupon '{couponCode}' found.");
        }

        return coupon;
    }
}
