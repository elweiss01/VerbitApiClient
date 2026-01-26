using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VerbitApiClient
{
    public partial class CreateOrderWindow : Window
    {
        private readonly HttpClient _httpClient;
        private string _bearerToken = string.Empty;
        private readonly StringBuilder _logBuilder = new StringBuilder();

        public CreateOrderWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
        }

        private void InputTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Input type selection handling can be added here if needed
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button during request
                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Creating Order...";
                ResponseBox.Text = "Sending request...";

                // Validate bearer token is generated
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token first");
                    return;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(OrderNameBox.Text))
                {
                    ShowError("Order Name is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(ClientTransactionIdBox.Text))
                {
                    ShowError("Client Transaction ID is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(UrlBox.Text))
                {
                    ShowError("RTMP URL is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(StreamKeyBox.Text))
                {
                    ShowError("Stream Key is required");
                    return;
                }

                // Build the request body
                var requestBody = BuildRequestBody();

                // Build the API URL
                string url = "https://orders.verbit.co/api/v2/orders";

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                // Add authorization header with bearer token
                request.Headers.Add("Authorization", $"Bearer {_bearerToken}");

                // Add content
                string jsonContent = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Log the request
                var headers = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_bearerToken}" },
                    { "Content-Type", "application/json" }
                };
                LogApiRequest("POST", url, jsonContent, headers);

                // Send request
                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                // Log the response
                LogApiResponse(response.StatusCode, responseContent);

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
                SubmitButton.Content = "Create Order";
            }
        }

        private Dictionary<string, object> BuildRequestBody()
        {
            var body = new Dictionary<string, object>
            {
                ["name"] = OrderNameBox.Text,
                ["client_transaction_id"] = ClientTransactionIdBox.Text
            };

            // Build input object with RTMP connection params
            var connectionParams = new Dictionary<string, object>
            {
                ["url"] = UrlBox.Text,
                ["stream_key"] = StreamKeyBox.Text
            };

            var inputType = (InputTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "rtmp_pull";
            var input = new Dictionary<string, object>
            {
                ["type"] = inputType,
                ["connection_params"] = connectionParams
            };

            body["input"] = input;

            // Build output/product object
            var product = new Dictionary<string, object>
            {
                ["modality"] = (ModalityCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "caption",
                ["language"] = LanguageBox.Text
            };

            var output = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["product"] = product
                }
            };

            body["output"] = output;

            // Add delivery if specified
            var deliveryType = (DeliveryTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "websocket";
            var delivery = new Dictionary<string, object>
            {
                ["type"] = deliveryType
            };

            if (!string.IsNullOrWhiteSpace(DeliveryDestinationBox.Text))
            {
                delivery["destination"] = DeliveryDestinationBox.Text;
            }

            body["delivery"] = new List<Dictionary<string, object>> { delivery };

            // Add contact details if provided
            if (!string.IsNullOrWhiteSpace(ContactNameBox.Text) || 
                !string.IsNullOrWhiteSpace(ContactEmailBox.Text) || 
                !string.IsNullOrWhiteSpace(ContactPhoneBox.Text))
            {
                var contactDetails = new Dictionary<string, object>();
                
                if (!string.IsNullOrWhiteSpace(ContactNameBox.Text))
                    contactDetails["name"] = ContactNameBox.Text;
                if (!string.IsNullOrWhiteSpace(ContactEmailBox.Text))
                    contactDetails["email"] = ContactEmailBox.Text;
                if (!string.IsNullOrWhiteSpace(ContactPhoneBox.Text))
                    contactDetails["phone"] = ContactPhoneBox.Text;

                body["contact_details"] = contactDetails;
            }

            // Add start time if provided
            if (!string.IsNullOrWhiteSpace(StartTimeBox.Text))
            {
                body["start_time"] = StartTimeBox.Text;
            }

            // Add metadata if provided
            if (!string.IsNullOrWhiteSpace(MetadataBox.Text))
            {
                try
                {
                    var metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(MetadataBox.Text);
                    if (metadata != null && metadata.Count > 0)
                    {
                        body["meta_data"] = metadata;
                    }
                }
                catch
                {
                    throw new Exception("Invalid JSON format for Metadata. Expected JSON object.");
                }
            }

            return body;
        }

        private void DisplayResponse(System.Net.HttpStatusCode statusCode, string content)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Status Code: {(int)statusCode} {statusCode}");
            sb.AppendLine();

            try
            {
                var jsonObject = JToken.Parse(content);
                sb.AppendLine(jsonObject.ToString(Formatting.Indented));

                if (statusCode == System.Net.HttpStatusCode.Created || statusCode == System.Net.HttpStatusCode.OK)
                {
                    sb.AppendLine();
                    sb.AppendLine("✓ Order created successfully!");

                    var orderId = jsonObject["id"]?.ToString();
                    if (!string.IsNullOrEmpty(orderId))
                    {
                        sb.AppendLine($"Order ID: {orderId}");
                    }
                }
            }
            catch
            {
                sb.AppendLine(content);
            }

            ResponseBox.Text = sb.ToString();
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
                string jsonContent = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Log the request
                var headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                };
                LogApiRequest("POST", url, jsonContent, headers);

                // Send request
                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                // Log the response
                LogApiResponse(response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    var jsonResponse = JObject.Parse(responseContent);
                    _bearerToken = jsonResponse["token"]?.ToString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(_bearerToken))
                    {
                        TokenStatusLabel.Text = "Bearer token generated successfully (valid for 24 hours)";
                        TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                        ResponseBox.Text = "Bearer token generated successfully! You can now create orders.";
                    }
                    else
                    {
                        ShowError("Failed to extract bearer token from response");
                    }
                }
                else
                {
                    _bearerToken = string.Empty;
                    TokenStatusLabel.Text = "Failed to generate bearer token";
                    TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                    ShowError($"Failed to generate bearer token: {response.StatusCode}\n{responseContent}");
                }
            }
            catch (Exception ex)
            {
                _bearerToken = string.Empty;
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

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all input fields
            ApiTokenBox.Clear();
            _bearerToken = string.Empty;
            TokenStatusLabel.Text = "No bearer token generated";
            TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Gray;

            OrderNameBox.Clear();
            ClientTransactionIdBox.Clear();
            UrlBox.Clear();
            StreamKeyBox.Clear();
            LanguageBox.Text = "en-US";
            ModalityCombo.SelectedIndex = 0;
            DeliveryTypeCombo.SelectedIndex = 0;
            DeliveryDestinationBox.Clear();
            StartTimeBox.Clear();
            ContactNameBox.Clear();
            ContactEmailBox.Clear();
            ContactPhoneBox.Clear();
            MetadataBox.Clear();
            ResponseBox.Clear();
            LoggingBox.Clear();
            _logBuilder.Clear();
            InputTypeCombo.SelectedIndex = 0;
        }

        private string GetPasswordBoxText(PasswordBox passwordBox)
        {
            return passwordBox.Password;
        }

        private void AddLog(string message)
        {
            _logBuilder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
            LoggingBox.Text = _logBuilder.ToString();
            LoggingBox.ScrollToEnd();
        }

        private void LogApiRequest(string method, string url, string requestBody, Dictionary<string, string>? headers = null)
        {
            AddLog("=== API REQUEST ===");
            AddLog($"Method: {method}");
            AddLog($"URL: {url}");

            if (headers != null && headers.Count > 0)
            {
                AddLog("Headers:");
                foreach (var header in headers)
                {
                    if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog($"  {header.Key}: Bearer [REDACTED]");
                    }
                    else
                    {
                        AddLog($"  {header.Key}: {header.Value}");
                    }
                }
            }

            AddLog("Request Body:");
            try
            {
                var jsonObject = JToken.Parse(requestBody);
                AddLog(jsonObject.ToString(Formatting.Indented));
            }
            catch
            {
                AddLog(requestBody);
            }
            AddLog("");
        }

        private void LogApiResponse(System.Net.HttpStatusCode statusCode, string responseContent)
        {
            AddLog("=== API RESPONSE ===");
            AddLog($"Status Code: {(int)statusCode} {statusCode}");

            try
            {
                var jsonObject = JToken.Parse(responseContent);
                AddLog("Response Body:");
                AddLog(jsonObject.ToString(Formatting.Indented));
            }
            catch
            {
                AddLog("Response Body:");
                AddLog(responseContent);
            }
            AddLog("");
        }

        private void ShowError(string message)
        {
            ResponseBox.Text = $"Error: {message}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
