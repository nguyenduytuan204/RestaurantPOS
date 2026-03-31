using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantPOS.API.Data;
using RestaurantPOS.API.Models;
using RestaurantPOS.API.Services;
using Microsoft.EntityFrameworkCore;

namespace RestaurantPOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VnPayController : ControllerBase
{
    private readonly IVnPayService _vnPayService;
    private readonly AppDbContext _db;
    private readonly IOrderService _orderService;

    public VnPayController(IVnPayService vnPayService, AppDbContext db, IOrderService orderService)
    {
        _vnPayService = vnPayService;
        _db = db;
        _orderService = orderService;
    }

    // POST /api/vnpay/create-payment/5
    [Authorize(Roles = "Admin, Manager, Cashier")]
    [HttpPost("create-payment/{orderId}")]
    public async Task<IActionResult> CreatePayment(int orderId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.Status <= 1);
        if (order == null) return NotFound(new { message = "Order không tồn tại hoặc đã thanh toán." });

        if (order.FinalAmount <= 0) return BadRequest(new { message = "Số tiền thanh toán phải lớn hơn 0." });

        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        string paymentUrl = _vnPayService.CreatePaymentUrl(order, ipAddress);

        return Ok(new { paymentUrl });
    }

    // GET /api/vnpay/vnpay-return
    [HttpGet("vnpay-return")]
    public async Task<IActionResult> VnPayReturn()
    {
        if (Request.Query.Count > 0)
        {
            string vnp_HashSecret = _vnPayService.ValidateSignature(Request.Query) ? "Success" : "Fail";
            
            if (vnp_HashSecret == "Success")
            {
                long vnp_Amount = Convert.ToInt64(Request.Query["vnp_Amount"]) / 100;
                long vnp_ResponseCode = Convert.ToInt64(Request.Query["vnp_ResponseCode"]);
                long vnp_TransactionStatus = Convert.ToInt64(Request.Query["vnp_TransactionStatus"]);
                int orderId = Convert.ToInt32(Request.Query["vnp_TxnRef"]);

                if (vnp_ResponseCode == 0 && vnp_TransactionStatus == 0)
                {
                    // Thanh toán thành công
                    await CompleteOrder(orderId, "VNPay");
                    return Redirect("/mobile_order.html?vnpay=success&orderId=" + orderId);
                }
                else
                {
                    // Thanh toán thất bại
                    return Redirect("/mobile_order.html?vnpay=fail&code=" + vnp_ResponseCode);
                }
            }
        }
        return Redirect("/mobile_order.html?vnpay=error");
    }

    // GET /api/vnpay/vnpay-ipn
    [HttpGet("vnpay-ipn")]
    public async Task<IActionResult> VnPayIpn()
    {
        // VNPay IPN logic (Simplified for this project)
        if (Request.Query.Count > 0)
        {
            if (_vnPayService.ValidateSignature(Request.Query))
            {
                int orderId = Convert.ToInt32(Request.Query["vnp_TxnRef"]);
                long vnp_ResponseCode = Convert.ToInt64(Request.Query["vnp_ResponseCode"]);
                
                if (vnp_ResponseCode == 0)
                {
                    await CompleteOrder(orderId, "VNPay");
                    return Ok(new { RspCode = "00", Message = "Confirm Success" });
                }
            }
        }
        return Ok(new { RspCode = "99", Message = "Invalid Signature" });
    }

    private async Task CompleteOrder(int orderId, string method)
    {
        var order = await _db.Orders.Include(o => o.DiningTable).FirstOrDefaultAsync(o => o.OrderID == orderId);
        if (order != null && order.Status <= 1)
        {
            order.Status = 2; // Paid
            order.CheckoutAt = DateTime.UtcNow.AddHours(7);
            order.Note = (order.Note ?? "") + " [Paid via " + method + "]";
            
            // Tìm hoặc tạo PaymentMethod VNPay (Giả định ID = 3 cho thực tế, ở đây ta gán tạm)
            order.PaymentMethodID = 3; 

            if (order.DiningTable != null)
            {
                order.DiningTable.Status = 0; // Free table
            }
            await _db.SaveChangesAsync();
        }
    }
}
