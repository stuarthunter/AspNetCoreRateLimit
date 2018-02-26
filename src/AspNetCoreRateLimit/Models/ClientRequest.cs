
namespace AspNetCoreRateLimit.Models
{
    /// <summary>
    /// Stores the client IP, ID, endpoint and verb
    /// </summary>
    public class ClientRequest
    {
        public string ClientIp { get; set; }

        public string ClientId { get; set; }

        public string HttpVerb { get; set; }

        public string Path { get; set; }
    }
}
