namespace Marketplace.Domain.Enums;

public enum UserStatus
{
    ACTIVE,
    INACTIVE,
    BANNED
}

public enum RoleName
{
    CUSTOMER,
    SELLER,
    ADMIN,
    SUPER_ADMIN
}

public enum ShopStatus
{
    PENDING,
    ACTIVE,
    REJECTED,
    BANNED
}

public enum CategoryStatus
{
    ACTIVE,
    HIDDEN
}

public enum ProductStatus
{
    ACTIVE,
    HIDDEN,
    DISCONTINUED,
    BANNED
}

public enum ProductVariantStatus
{
    ACTIVE,
    INACTIVE
}

public enum OrderStatus
{
    PENDING,
    READY_FOR_PICKUP,
    SHIPPING,
    DELIVERED,
    COMPLETED,
    CANCELLED,
    RETURN_REQUESTED,
    RETURNED,
    CONFIRMED
}

public enum PaymentMethod
{
    COD,
    VNPAY,
    MOMO,
    CREDIT_CARD
}

public enum PaymentStatus
{
    UNPAID,
    PAID,
    FAILED,
    REFUNDED,
    PARTIALLY_REFUNDED
}

public enum DiscountType
{
    PERCENTAGE,
    FIXED_AMOUNT
}

public enum VoucherStatus
{
    ACTIVE,
    INACTIVE,
    EXPIRED
}

public enum ReviewStatus
{
    ACTIVE,
    HIDDEN
}

public enum ReturnRefundRequestStatus
{
    PENDING,
    APPROVED,
    REJECTED,
    DISPUTED,
    RESOLVED
}

public enum ComplaintStatus
{
    OPEN,
    IN_PROGRESS,
    RESOLVED,
    CLOSED
}

public enum ActionType
{
    VIEW,
    CLICK,
    ADD_TO_CART,
    PURCHASE,
    WISHLIST_ADD,
    WISHLIST_REMOVE,
    REVIEW
}

public enum NotificationType
{
    ORDER,
    PROMOTION,
    SYSTEM,
    DISPUTE
}
