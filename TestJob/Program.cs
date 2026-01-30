using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static async Task Main()
    {
        string apiToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbiI6ImdBQUFBQUJwZkxLLVRzNVNqQkNwdEJPU3hGVGhQSHVFTzBaVG5IR1doR05TcXNNLVdfaFpMNGk2MTFpUEl0cEFnSXFVazhCbFFkaWthMUtrZUJ2TzlUX3c3bDFzaDAtYzcwS1BzNFdmcWZjVjJ1ay14S2tOZVh6UmJtaUdRcWszRmVtQkRvekpzSlNFSFBDQkd5WkJabjBiTW50TUdQTi1qYmJmYlI5MEQ2YmgzaHVOZDdub3ZZb190a2loQTJFdVVuT1czdDZWOVBhYzRIdlRrcUtPaVc0aHRNNEtyVmlVdjVueG4tTHFUa2V6X0ptbkFTaE1OS2M9IiwiaWF0IjoxNzY5Nzc5OTAyfQ.J6wjCIsigR-QQT_hdhoNhnGx7YlCKsU4Z-7stflkRMF8NLGzVxW7R7NGKaXd7De_pC3iw1q7Zoash2GYTRCkiJICHRXBklRgYJ69Py2LlFsjG7OS4cGuqi6u5ZiJkFxrY55VHOWzzs4zE6Mv4Ypr3_sq0QT-YdWNZDfsIxDfnKKOeHAiMcg0lkeWeltGgdwvOVX9Adjh_0TtYg2ZXB3VGzHFppyfBUD0x6fucQWC9UFQXLxRigGixyZlIQwCMez2N299bBFkC7UBebV-Ca1XPbXv5-dybfYADF2Flh1lbmEpjmqkdmxizNZIMXoVkj1WduF5G70M2c29pzyIN1Zr0A";
        
        using (var httpClient = new HttpClient())
        {
            try
            {
                // Step 1: Generate bearer token
                Console.WriteLine("Step 1: Generating bearer token...");
                var authUrl = "https://users.verbit.co/api/v1/auth";
                var authBody = new { data = new { api_key = apiToken } };
                var authRequest = new HttpRequestMessage(HttpMethod.Post, authUrl);
                authRequest.Content = new StringContent(JsonConvert.SerializeObject(authBody), Encoding.UTF8, "application/json");
                
                var authResponse = await httpClient.SendAsync(authRequest);
                var authContent = await authResponse.Content.ReadAsStringAsync();
                var authJson = JObject.Parse(authContent);
                string bearerToken = authJson["token"]?.ToString() ?? "";
                Console.WriteLine("✓ Bearer token generated\n");
                
                // Step 2: Load profiles
                Console.WriteLine("Step 2: Loading profiles...");
                var profilesUrl = "https://api.verbit.co/api/profiles?v=4";
                var profilesRequest = new HttpRequestMessage(HttpMethod.Get, profilesUrl);
                profilesRequest.Headers.Add("Authorization", $"Bearer {bearerToken}");
                
                var profilesResponse = await httpClient.SendAsync(profilesRequest);
                var profilesContent = await profilesResponse.Content.ReadAsStringAsync();
                var profilesJson = JObject.Parse(profilesContent);
                var profiles = profilesJson["profiles"] as JArray;
                string selectedProfile = profiles?[0]?["name"]?.ToString() ?? "default";
                Console.WriteLine($"✓ Loaded {profiles?.Count} profile(s)");
                Console.WriteLine($"  Using: {selectedProfile}\n");
                
                // Step 3: Create job
                Console.WriteLine("Step 3: Creating test job...");
                var jobUrl = "https://api.verbit.co/api/job/new";
                var startAt = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss.ffffff") + "-08:00";
                
                var jobBody = new
                {
                    name = $"Test Job {DateTime.Now:HHmmss}",
                    client_transaction_id = Guid.NewGuid().ToString(),
                    profile = selectedProfile,
                    input = new
                    {
                        language = "en-US",
                        type = "web_url",
                        url_type = "zoom",
                        service_type = "live",
                        schedule = new
                        {
                            start_at = startAt,
                            max_duration = 900,
                            timezone = "America/Los_Angeles"
                        },
                        connection_params = new
                        {
                            url = "https://zoom.us/meeting/123456789"
                        }
                    },
                    output = new object[]
                    {
                        new
                        {
                            product = new
                            {
                                type = "captions",
                                tier = "automatic",
                                service_type = "live",
                                target_languages = new[] { "en-US" }
                            }
                        }
                    }
                };
                
                var jobRequest = new HttpRequestMessage(HttpMethod.Post, jobUrl);
                jobRequest.Headers.Add("Authorization", $"Bearer {bearerToken}");
                string jobJson = JsonConvert.SerializeObject(jobBody, Formatting.Indented);
                Console.WriteLine("\nRequest body:");
                Console.WriteLine(jobJson);
                jobRequest.Content = new StringContent(jobJson, Encoding.UTF8, "application/json");
                
                var jobResponse = await httpClient.SendAsync(jobRequest);
                var jobContent = await jobResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"\n✓ Response Status: {jobResponse.StatusCode}");
                Console.WriteLine("\nResponse body:");
                
                try
                {
                    var jobResponseJson = JToken.Parse(jobContent);
                    Console.WriteLine(jobResponseJson.ToString(Formatting.Indented));
                    
                    var jobId = jobResponseJson["job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                    {
                        Console.WriteLine($"\n✓✓✓ SUCCESS! Job created with ID: {jobId}");
                    }
                }
                catch
                {
                    Console.WriteLine(jobContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
