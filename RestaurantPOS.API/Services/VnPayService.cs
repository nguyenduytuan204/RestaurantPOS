using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using RestaurantPOS.API.Models;

namespace RestaurantPOS.API.Services;

public interface IVnPayService
{
    string CreatePaymentUrl(Order order, string ipAddress);
    bool ValidateSignature(IQueryCollection query);
}

public class VnPayService : IVnPayService
{
    private readonly IConfiguration _config;

    public VnPayService(IConfiguration config)
    {
        _config = config;
    }

    public string CreatePaymentUrl(Order order, string ipAddress)
    {
        var vnp_TmnCode = _config["VnPay:TmnCode"] ?? "";
        var vnp_HashSecret = _config["VnPay:HashSecret"] ?? "";
        var vnp_BaseUrl = _config["VnPay:BaseUrl"] ?? "";
        var vnp_ReturnUrl = _config["VnPay:ReturnUrl"] ?? "";

        var vnp_CreateDate = DateTime.Now.ToString("yyyyMMddHHmmss");
        var vnp_TxnRef = order.OrderID.ToString();
        var vnp_OrderInfo = $"Thanh toan don hang {order.OrderID}";
        var vnp_Amount = ((long)(order.FinalAmount * 100)).ToString(); // VNPay uses cents (x100)
        
        var vnp_Params = new SortedList<string, string>(new VnPayComparer());
        vnp_Params.Add("vnp_Version", "2.1.0");
        vnp_Params.Add("vnp_Command", "pay");
        vnp_Params.Add("vnp_TmnCode", vnp_TmnCode);
        vnp_Params.Add("vnp_Amount", vnp_Amount);
        vnp_Params.Add("vnp_CreateDate", vnp_CreateDate);
        vnp_Params.Add("vnp_CurrCode", "VND");
        vnp_Params.Add("vnp_IpAddr", ipAddress);
        vnp_Params.Add("vnp_Locale", "vn");
        vnp_Params.Add("vnp_OrderInfo", vnp_OrderInfo);
        vnp_Params.Add("vnp_OrderType", "other");
        vnp_Params.Add("vnp_ReturnUrl", vnp_ReturnUrl);
        vnp_Params.Add("vnp_TxnRef", vnp_TxnRef);

        StringBuilder data = new StringBuilder();
        foreach (KeyValuePair<string, string> kv in vnp_Params)
        {
            if (!string.IsNullOrEmpty(kv.Value))
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }
        }

        string queryString = data.ToString();
        queryString = queryString.Remove(queryString.Length - 1); // remove last &

        string rawData = queryString;
        string vnp_SecureHash = HmacSHA512(vnp_HashSecret, rawData);
        string paymentUrl = vnp_BaseUrl + "?" + queryString + "&vnp_SecureHash=" + vnp_SecureHash;

        return paymentUrl;
    }

    public bool ValidateSignature(IQueryCollection query)
    {
        var vnp_HashSecret = _config["VnPay:HashSecret"] ?? "";
        var vnp_SecureHash = query["vnp_SecureHash"].ToString();
        
        var vnp_Params = new SortedList<string, string>(new VnPayComparer());
        foreach (var key in query.Keys)
        {
            if (key.StartsWith("vnp_") && key != "vnp_SecureHash")
            {
                vnp_Params.Add(key, query[key].ToString());
            }
        }

        StringBuilder data = new StringBuilder();
        foreach (KeyValuePair<string, string> kv in vnp_Params)
        {
            if (!string.IsNullOrEmpty(kv.Value))
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }
        }

        string queryString = data.ToString();
        queryString = queryString.Remove(queryString.Length - 1); // remove last &

        string checkSum = HmacSHA512(vnp_HashSecret, queryString);
        return checkSum.Equals(vnp_SecureHash, StringComparison.InvariantCultureIgnoreCase);
    }

    private string HmacSHA512(string key, string inputData)
    {
        var hash = new StringBuilder();
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
        using (var hmac = new HMACSHA512(keyBytes))
        {
            byte[] hashValue = hmac.ComputeHash(inputBytes);
            foreach (var theByte in hashValue)
            {
                hash.Append(theByte.ToString("x2"));
            }
        }
        return hash.ToString();
    }
}

public class VnPayComparer : IComparer<string?>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return string.Compare(x, y, StringComparison.Ordinal);
    }
}
