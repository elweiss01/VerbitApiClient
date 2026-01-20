using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VerbitApiClient
{
    public partial class ConnectionPlanWindow : Window
    {
        private readonly HttpClient _httpClient;

        public ConnectionPlanWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button during request
                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Updating...";
                ResponseBox.Text = "Sending request...";

                // Validate required fields
                if (string.IsNullOrWhiteSpace(GetPasswordBoxText(BearerTokenBox)))
                {
                    ShowError("Bearer Token is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(OrderIdBox.Text))
                {
                    ShowError("Order ID is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(ConnectionPlanIdBox.Text))
                {
                    ShowError("Connection Plan ID is required");
                    return;
                }

                // Validate Connection Plan ID is a number
                if (!int.TryParse(ConnectionPlanIdBox.Text, out int connectionPlanId))
                {
                    ShowError("Connection Plan ID must be a valid integer");
                    return;
                }

                // Build the API URL
                string orderId = OrderIdBox.Text.Trim();
                string url = $"https://realtime.verbit.co/api/v1/session/{Uri.EscapeDataString(orderId)}/connection_plan";

                // Build request body
                var requestBody = new Dictionary<string, object>
                {
                    ["connection_plan_id"] = connectionPlanId
                };

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                // Add authorization header
                string bearerToken = GetPasswordBoxText(BearerTokenBox);
                request.Headers.Add("Authorization", $"Bearer {bearerToken}");

                // Add content
                string jsonContent = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send request
                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                // Display response
                DisplayResponse(response.StatusCode, responseContent);
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                // Re-enable button
                SubmitButton.IsEnabled = true;
                SubmitButton.Content = "Update Plan";
            }
        }

        private void DisplayResponse(System.Net.HttpStatusCode statusCode, string content)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Status Code: {(int)statusCode} {statusCode}");
            sb.AppendLine();

            try
            {
                var jsonObject = JToken.Parse(content);
                sb.AppendLine(jsonObject.ToString(Formatting.Indented));

                if (statusCode == System.Net.HttpStatusCode.OK)
                {
                    sb.AppendLine();
                    sb.AppendLine("✓ Connection plan updated successfully!");

                    // Extract connection plan details from response
                    var state = jsonObject["state"];
                    if (state != null)
                    {
                        var connectionPlan = state["connection_plan"];
                        if (connectionPlan != null)
                        {
                            var planId = connectionPlan["id"]?.ToString();
                            var planName = connectionPlan["name"]?.ToString();

                            if (!string.IsNullOrEmpty(planId))
                            {
                                sb.AppendLine($"Connection Plan ID: {planId}");
                            }
                            if (!string.IsNullOrEmpty(planName))
                            {
                                sb.AppendLine($"Connection Plan Name: {planName}");
                            }
                        }
                    }
                }
                else if (statusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ Validation Error");
                    sb.AppendLine("Please check your input parameters.");
                }
            }
            catch
            {
                sb.AppendLine(content);
            }

            ResponseBox.Text = sb.ToString();
        }

        private void ShowError(string message)
        {
            ResponseBox.Text = $"Error: {message}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all input fields
            BearerTokenBox.Clear();
            OrderIdBox.Clear();
            ConnectionPlanIdBox.Clear();
            ResponseBox.Clear();
        }

        private string GetPasswordBoxText(System.Windows.Controls.PasswordBox passwordBox)
        {
            return passwordBox.Password;
        }
    }
}
