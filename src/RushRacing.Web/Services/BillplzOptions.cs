namespace RushRacing.Web.Services;

public class BillplzOptions
{
    public string BaseUrl { get; set; } = "https://www.billplz-sandbox.com/api/v3/";
    public string ApiSecretKey { get; set; } = string.Empty;
    public string XSignatureKey { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
}
