namespace RestaurantPOS.API.Models
{
    public class Area
    {
        public int AreaID { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<DiningTable> DiningTables { get; set; } = new List<DiningTable>();
    }

    public class DiningTable
    {
        public int TableID { get; set; }
        public int AreaID { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int Capacity { get; set; } = 4;
        public byte Status { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public Area Area { get; set; } = null!;
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    public class Category
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class Product
    {
        public int ProductID { get; set; }
        public int CategoryID { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public Category Category { get; set; } = null!;
    }

    public class Order
    {
        public int OrderID { get; set; }
        public int TableID { get; set; }
        public int UserID { get; set; }
        public int? PaymentMethodID { get; set; }
        public byte Status { get; set; } = 0;
        public decimal TotalAmount { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public decimal FinalAmount { get; set; } = 0;
        public decimal? CustomerPaid { get; set; }
        public decimal? ChangeAmount { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CheckoutAt { get; set; }
        public DiningTable DiningTable { get; set; } = null!;
        public User? User { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }

    public class OrderDetail
    {
        public int OrderDetailID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public string? Note { get; set; }
        public Order Order { get; set; } = null!;
        public Product Product { get; set; } = null!;
        public decimal SubTotal => Quantity * UnitPrice;
    }

    public class PaymentMethod
    {
        public int PaymentMethodID { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class User
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public byte Role { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
