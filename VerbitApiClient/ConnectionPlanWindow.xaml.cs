using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VerbitApiClient.Models;

namespace VerbitApiClient
{
    public partial class ConnectionPlanWindow : Window
    {
        private readonly HttpClient _httpClient;
        private string _bearerToken = string.Empty;
        private Dictionary<string, string> _connectionPlanMap = new Dictionary<string, string>(); // Maps display name to ID

        public ConnectionPlanWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            ConnectionPlanComboBox.SelectionChanged += ConnectionPlanComboBox_SelectionChanged;
        }

        private void ConnectionPlanComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConnectionPlanComboBox.SelectedItem != null)
            {
                string selectedName = ConnectionPlanComboBox.SelectedItem.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(selectedName) && _connectionPlanMap.TryGetValue(selectedName, out string? planId))
                {
                    SelectedPlanIdLabel.Text = $"Selected Plan ID: {planId}";
                }
            }
            else
            {
                SelectedPlanIdLabel.Text = "Selected Plan ID: (None)";
            }
        }

        private async void FetchConnectionPlansButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate bearer token is generated
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token first");
                    return;
                }

                // Disable button during request
                FetchConnectionPlansButton.IsEnabled = false;
                FetchConnectionPlansButton.Content = "Fetching...";
                ConnectionPlansStatusLabel.Text = "Fetching connection plans...";
                ConnectionPlansStatusLabel.Foreground = System.Windows.Media.Brushes.Orange;

                // Build the API URL
                string url = "https://orders.verbit.co/api/v2/customer";

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add authorization header with stored bearer token
                request.Headers.Add("Authorization", $"Bearer {_bearerToken}");

                // Send request
                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    var apiResponse = JsonConvert.DeserializeObject<CustomerApiResponse>(responseContent);

                    if (apiResponse?.Customer?.ConnectionPlans != null && apiResponse.Customer.ConnectionPlans.Count > 0)
                    {
                        // Clear existing items
                        ConnectionPlanComboBox.Items.Clear();
                        _connectionPlanMap.Clear();

                        // Populate ComboBox with connection plan names and map to IDs
                        foreach (var plan in apiResponse.Customer.ConnectionPlans)
                        {
                            if (!string.IsNullOrWhiteSpace(plan.Name) && !string.IsNullOrWhiteSpace(plan.Id))
                            {
                                ConnectionPlanComboBox.Items.Add(plan.Name);
                                _connectionPlanMap[plan.Name] = plan.Id;
                            }
                        }

                        ConnectionPlansStatusLabel.Text = $"Successfully loaded {apiResponse.Customer.ConnectionPlans.Count} connection plan(s)";
                        ConnectionPlansStatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                        ResponseBox.Text = $"Successfully loaded {apiResponse.Customer.ConnectionPlans.Count} connection plan(s) from your account.";

                        // Auto-select the first plan if available
                        if (ConnectionPlanComboBox.Items.Count > 0)
                        {
                            ConnectionPlanComboBox.SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        ConnectionPlansStatusLabel.Text = "No connection plans found for your account";
                        ConnectionPlansStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                        ShowError("No connection plans found for your account");
                    }
                }
                else
                {
                    ConnectionPlansStatusLabel.Text = "Failed to fetch connection plans";
                    ConnectionPlansStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                    ShowError($"Failed to fetch connection plans: {response.StatusCode}\n{responseContent}");
                }
            }
            catch (Exception ex)
            {
                ConnectionPlansStatusLabel.Text = "Error fetching connection plans";
                ConnectionPlansStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                ShowError($"Error fetching connection plans: {ex.Message}");
            }
            finally
            {
                // Re-enable button
                FetchConnectionPlansButton.IsEnabled = true;
                FetchConnectionPlansButton.Content = "Fetch Plans";
            }
        }

        private async void GenerateBearerTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate API token is provided
                if (string.IsNullOrWhiteSpace(GetPasswordBoxText(ApiTokenBox)))
                {
                    ShowError("Please enter your API Token before generating bearer token");
                    return;
                }

                // Disable button during request
                GenerateBearerTokenButton.IsEnabled = false;
                GenerateBearerTokenButton.Content = "Generating...";
                TokenStatusLabel.Text = "Generating bearer token...";
                TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Orange;

                // Build the auth endpoint URL
                string url = "https://users.verbit.co/api/v1/auth";

                // Create request body
                var requestBody = new
                {
                    data = new
                    {
                        api_key = GetPasswordBoxText(ApiTokenBox)
                    }
                };

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                string jsonContent = JsonConvert.SerializeObject(requestBody);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send request
                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    var jsonResponse = JObject.Parse(responseContent);
                    _bearerToken = jsonResponse["token"]?.ToString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(_bearerToken))
                    {
                        TokenStatusLabel.Text = "Bearer token generated successfully (valid for 24 hours)";
                        TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                        FetchConnectionPlansButton.IsEnabled = true;
                        ResponseBox.Text = "Bearer token generated successfully! You can now fetch and select connection plans.";
                    }
                    else
                    {
                        ShowError("Failed to extract bearer token from response");
                    }
                }
                else
                {
                    _bearerToken = string.Empty;
                    FetchConnectionPlansButton.IsEnabled = false;
                    TokenStatusLabel.Text = "Failed to generate bearer token";
                    TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                    ShowError($"Failed to generate bearer token: {response.StatusCode}\n{responseContent}");
                }
            }
            catch (Exception ex)
            {
                _bearerToken = string.Empty;
                FetchConnectionPlansButton.IsEnabled = false;
                TokenStatusLabel.Text = "Error generating bearer token";
                TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                ShowError($"Error generating bearer token: {ex.Message}");
            }
            finally
            {
                // Re-enable button
                GenerateBearerTokenButton.IsEnabled = true;
                GenerateBearerTokenButton.Content = "Generate Bearer Token";
            }
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button during request
                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Updating...";
                ResponseBox.Text = "Sending request...";

                // Validate bearer token is generated
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token first");
                    return;
                }

                if (string.IsNullOrWhiteSpace(OrderIdBox.Text))
                {
                    ShowError("Order ID is required");
                    return;
                }

                if (ConnectionPlanComboBox.SelectedItem == null)
                {
                    ShowError("Please select a connection plan");
                    return;
                }

                // Get the selected connection plan ID from the map
                string selectedPlanName = ConnectionPlanComboBox.SelectedItem?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(selectedPlanName) || !_connectionPlanMap.TryGetValue(selectedPlanName, out string? connectionPlanId))
                {
                    ShowError("Invalid connection plan selected");
                    return;
                }

                // Build the API URL
                string orderId = OrderIdBox.Text.Trim();
                string url = $"https://realtime.verbit.co/api/v1/session/{Uri.EscapeDataString(orderId)}/connection_plan";

                // Build request body - send the connection plan ID as-is (it might be a GUID)
                var requestBody = new Dictionary<string, object>
                {
                    ["connection_plan_id"] = connectionPlanId
                };

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                // Add authorization header with stored bearer token
                request.Headers.Add("Authorization", $"Bearer {_bearerToken}");

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
            ApiTokenBox.Clear();
            _bearerToken = string.Empty;
            TokenStatusLabel.Text = "No bearer token generated";
            TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Gray;
            OrderIdBox.Clear();
            ConnectionPlanComboBox.Items.Clear();
            ConnectionPlanComboBox.SelectedItem = null;
            _connectionPlanMap.Clear();
            SelectedPlanIdLabel.Text = "Selected Plan ID: (None)";
            ConnectionPlansStatusLabel.Text = "Click 'Fetch Plans' to load available connection plans";
            ConnectionPlansStatusLabel.Foreground = System.Windows.Media.Brushes.Gray;
            ResponseBox.Clear();
            FetchConnectionPlansButton.IsEnabled = false;
        }

        private string GetPasswordBoxText(System.Windows.Controls.PasswordBox passwordBox)
        {
            return passwordBox.Password;
        }
    }
}
