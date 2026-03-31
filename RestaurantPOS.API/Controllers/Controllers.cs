// ============================================================
//  Controllers/  —  Nhận HTTP Request, trả HTTP Response
//  Controller KHÔNG chứa logic — chỉ gọi Service
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.API.DTOs;
using RestaurantPOS.API.Services;

namespace RestaurantPOS.API.Controllers;


// ════════════════════════════════════════════════════════════
//  GET /api/areas          → Lấy sơ đồ tất cả khu + bàn
//  POST /api/areas/{id}/tables/{tableId}/open  → Mở bàn mới
// ════════════════════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
public class AreasController : ControllerBase
{
    private readonly ITableService _tableService;

    public AreasController(ITableService tableService)
        => _tableService = tableService;

    // GET /api/areas
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpGet]
    public async Task<IActionResult> GetFloorMap()
    {
        var result = await _tableService.GetFloorMapAsync();
        return Ok(result);
    }

    // GET /api/areas/list
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpGet("list")]
    public async Task<IActionResult> GetAreas()
    {
        var result = await _tableService.GetAreasAsync();
        return Ok(result);
    }

    // POST /api/areas
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> AddArea([FromBody] UpsertAreaDto dto)
    {
        var result = await _tableService.AddAreaAsync(dto);
        return CreatedAtAction(nameof(GetAreas), new { id = result.AreaID }, result);
    }

    // PUT /api/areas/{id}
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateArea(int id, [FromBody] UpsertAreaDto dto)
    {
        try
        {
            var result = await _tableService.UpdateAreaAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // DELETE /api/areas/{id}
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteArea(int id)
    {
        var success = await _tableService.DeleteAreaAsync(id);
        return success ? NoContent() : NotFound();
    }

    // POST /api/areas/tables/{tableId}/open
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpPost("tables/{tableId}/open")]
    public async Task<IActionResult> OpenTable(int tableId, [FromQuery] int? userId = null)
    {
        try
        {
            // Tự động lấy UserID từ Token nếu không truyền vào query
            if (!userId.HasValue || userId <= 1)
            {
                var claimId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(claimId, out int tid)) userId = tid;
            }

            var order = await _tableService.CreateOrderAsync(tableId, userId ?? 1);
            return CreatedAtAction(nameof(OpenTable), new { tableId }, order);
        }
        catch (InvalidOperationException ex)
        {
            // Bàn đã có người — trả 409 Conflict
            return Conflict(new { message = ex.Message });
        }
    }

    // --- TABLE MANAGEMENT ---

    // GET /api/areas/{id}/tables
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpGet("{id}/tables")]
    public async Task<IActionResult> GetTables(int id)
    {
        var result = await _tableService.GetTablesByAreaAsync(id);
        return Ok(result);
    }

    // POST /api/areas/tables
    [Authorize(Roles = "Admin")]
    [HttpPost("tables")]
    public async Task<IActionResult> AddTable([FromBody] UpsertTableDto dto)
    {
        var result = await _tableService.AddTableAsync(dto);
        return Ok(result);
    }

    // PUT /api/areas/tables/{tableId}
    [Authorize(Roles = "Admin")]
    [HttpPut("tables/{tableId}")]
    public async Task<IActionResult> UpdateTable(int tableId, [FromBody] UpsertTableDto dto)
    {
        try
        {
            var result = await _tableService.UpdateTableAsync(tableId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // DELETE /api/areas/tables/{tableId}
    [Authorize(Roles = "Admin")]
    [HttpDelete("tables/{tableId}")]
    public async Task<IActionResult> DeleteTable(int tableId)
    {
        var success = await _tableService.DeleteTableAsync(tableId);
        return success ? NoContent() : NotFound();
    }
}


// ════════════════════════════════════════════════════════════
//  GET /api/products                → Toàn bộ thực đơn
//  GET /api/products?categoryId=1   → Lọc theo danh mục
// ════════════════════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
        => _productService = productService;

    // GET /api/products
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int? categoryId = null)
    {
        var products = await _productService.GetProductsAsync(categoryId);
        return Ok(products);
    }

    // GET /api/products/categories
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _productService.GetCategoriesAsync();
        return Ok(categories);
    }

    // POST /api/products
    [Authorize(Roles = "Admin, Manager")]
    [HttpPost]
    public async Task<IActionResult> AddProduct([FromBody] UpsertProductDto dto)
    {
        try
        {
            var product = await _productService.AddProductAsync(dto);
            return CreatedAtAction(nameof(GetProducts), new { id = product.ProductID }, product);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT /api/products/{id}
    [Authorize(Roles = "Admin, Manager")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpsertProductDto dto)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(id, dto);
            return Ok(product);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PATCH /api/products/{id}/toggle
    [Authorize(Roles = "Admin, Manager")]
    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleProduct(int id)
    {
        var success = await _productService.ToggleProductAsync(id);
        return success ? NoContent() : NotFound();
    }

    // DELETE /api/products/{id}
    [Authorize(Roles = "Admin, Manager")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var success = await _productService.DeleteProductAsync(id);
        return success ? NoContent() : NotFound();
    }

    // --- CATEGORY MANAGEMENT ---

    // POST /api/products/categories
    [Authorize(Roles = "Admin, Manager")]
    [HttpPost("categories")]
    public async Task<IActionResult> AddCategory([FromBody] UpsertCategoryDto dto)
    {
        var result = await _productService.AddCategoryAsync(dto);
        return Ok(result);
    }

    // PUT /api/products/categories/{id}
    [Authorize(Roles = "Admin, Manager")]
    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpsertCategoryDto dto)
    {
        try
        {
            var result = await _productService.UpdateCategoryAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // DELETE /api/products/categories/{id}
    [Authorize(Roles = "Admin, Manager")]
    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        try
        {
            var success = await _productService.DeleteCategoryAsync(id);
            return success ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}


// ════════════════════════════════════════════════════════════
//  GET    /api/orders/{id}               → Xem chi tiết order
//  POST   /api/orders/{id}/items         → Thêm món
//  DELETE /api/orders/{id}/items/{detailId} → Xóa món
//  POST   /api/orders/{id}/checkout      → Thanh toán
// ════════════════════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
public class PaymentMethodsController : ControllerBase
{
    private readonly IOrderService _orderService;
    public PaymentMethodsController(IOrderService orderService) => _orderService = orderService;

    [Authorize(Roles = "Admin, Manager, Cashier")]
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _orderService.GetPaymentMethodsAsync());
}

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
        => _orderService = orderService;

    // GET /api/orders/5
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(int orderId)
    {
        try
        {
            var order = await _orderService.GetOrderAsync(orderId);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // POST /api/orders/5/items
    // Body: { "productId": 3, "quantity": 2, "note": "Ít đá" }
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpPost("{orderId}/items")]
    public async Task<IActionResult> AddItem(int orderId, [FromBody] AddItemDto dto)
    {
        try
        {
            var order = await _orderService.AddItemAsync(orderId, dto);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // DELETE /api/orders/5/items/12
    [Authorize(Roles = "Admin, Manager, Cashier, Waiter")]
    [HttpDelete("{orderId}/items/{orderDetailId}")]
    public async Task<IActionResult> RemoveItem(int orderId, int orderDetailId)
    {
        var removed = await _orderService.RemoveItemAsync(orderId, orderDetailId);
        return removed ? NoContent() : NotFound();
    }

    // POST /api/orders/5/checkout
    // Body: { "paymentMethodId": 1, "discount": 0, "customerPaid": 200000 }
    [Authorize(Roles = "Admin, Manager, Cashier")]
    [HttpPost("{orderId}/checkout")]
    public async Task<IActionResult> Checkout(int orderId, [FromBody] CheckoutRequestDto dto)
    {
        try
        {
            var result = await _orderService.CheckoutAsync(orderId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    public class UpdateStaffDto { public int StaffId { get; set; } }

    // PUT /api/orders/5/staff
    [Authorize(Roles = "Admin, Manager, Cashier")]
    [HttpPut("{orderId}/staff")]
    public async Task<IActionResult> UpdateStaff(int orderId, [FromBody] UpdateStaffDto dto)
    {
        try
        {
            var result = await _orderService.UpdateOrderStaffAsync(orderId, dto.StaffId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}


// ════════════════════════════════════════════════════════════
//  GET /api/dashboard   → Tổng quan doanh thu hôm nay
// ════════════════════════════════════════════════════════════
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _svc;
    public DashboardController(IDashboardService svc) => _svc = svc;

    [Authorize(Roles = "Admin, Manager")]
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _svc.GetDashboardAsync();
        return Ok(result);
    }
}
