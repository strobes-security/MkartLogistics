namespace MkartLogistics.API.Models;

public class Order
{
    public int Id { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public string OriginCity { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TierId { get; set; } = "standard";
}

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CarrierId { get; set; } = string.Empty;
    public DateTime? EstimatedDelivery { get; set; }
}

public class Vehicle
{
    public int Id { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class WarehouseItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class OrderSearchRequest
{
    public string? Query { get; set; }
    public string? Status { get; set; }
    public string? City { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class ReportRequest
{
    public string ReportType { get; set; } = string.Empty;
    public string Format { get; set; } = "pdf";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? OutputPath { get; set; }
}

public class WebhookPayload
{
    public string Event { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string? Data { get; set; }
}

public class NotificationRequest
{
    public string Recipient { get; set; } = string.Empty;
    public string Channel { get; set; } = "sms";
    public string Message { get; set; } = string.Empty;
    public string? TemplateId { get; set; }
}
