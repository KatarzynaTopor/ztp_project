namespace QuickBite.API.Models;

public enum OrderStatus
{
    Pending = 0,
    Accepted = 1,
    InPreparation = 2,
    ReadyForPickup = 3,
    OutForDelivery = 4,
    Delivered = 5,
    Rejected = 6,
    Cancelled = 7
}
