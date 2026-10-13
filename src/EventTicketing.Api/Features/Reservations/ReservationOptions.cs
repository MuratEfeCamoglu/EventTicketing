using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Api.Features.Reservations;

/// <summary><c>Reservations:*</c> keys (ISKELET §5.14).</summary>
public sealed class ReservationOptions
{
    public const string Section = "Reservations";

    [Range(1, 1440)]
    public int HoldMinutes { get; set; } = 10;

    [Range(1, 100)]
    public int MaxSeatsPerReservation { get; set; } = 6;

    /// <summary>Period of <see cref="ReservationExpiryService"/> (v2).</summary>
    [Range(1, 3600)]
    public int ExpirySweepIntervalSeconds { get; set; } = 30;
}
