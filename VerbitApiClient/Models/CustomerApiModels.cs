using System.Collections.Generic;
using Newtonsoft.Json;

namespace VerbitApiClient.Models
{
    public class CustomerApiResponse
    {
        [JsonProperty("customer")]
        public Customer? Customer { get; set; }
    }

    public class Customer
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("platform_id")]
        public int PlatformId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("connection_plans")]
        public List<ConnectionPlan> ConnectionPlans { get; set; } = new List<ConnectionPlan>();

        [JsonProperty("products")]
        public List<Product> Products { get; set; } = new List<Product>();
    }

    public class ConnectionPlan
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("customer_id")]
        public string? CustomerId { get; set; }

        [JsonProperty("facility_id")]
        public int FacilityId { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }

        [JsonProperty("is_vchip_enabled")]
        public bool IsVchipEnabled { get; set; }

        [JsonProperty("encoder_types")]
        public List<string> EncoderTypes { get; set; } = new List<string>();

        [JsonProperty("is_connection_plan")]
        public bool IsConnectionPlan { get; set; }

        [JsonProperty("concurrent_connections")]
        public int ConcurrentConnections { get; set; }

        public override string ToString()
        {
            return Name ?? "Unknown";
        }
    }

    public class Product
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }
    }
}
