using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VerbitApiClient
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private string _bearerToken = string.Empty;
        private readonly StringBuilder _logBuilder = new StringBuilder();

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
        }

        private async Task LoadProfilesAsync()
        {
            SpinnerOverlay.Visibility = Visibility.Visible;
            ProfilesLoadingBar.Visibility = Visibility.Visible;
            LoadProfilesButton.IsEnabled = false;
            LoadProfilesButton.Content = "Loading...";
            ProfilesCombo.Items.Clear();

            try
            {
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token before loading profiles");
                    return;
                }

                string url = "https://api.verbit.co/api/profiles?v=4";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {_bearerToken}");

                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var json = JToken.Parse(responseContent);
                    var profiles = json["profiles"]?
                        .Select(p => p["name"]?.ToString())
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .ToList() ?? new List<string>();

                    if (profiles != null && profiles.Count > 0)
                    {
                        foreach (var name in profiles)
                        {
                            ProfilesCombo.Items.Add(name);
                        }
                        ProfilesCombo.SelectedIndex = 0;
                        ResponseBox.Text = $"Loaded {profiles.Count} profile(s).";
                    }
                    else
                    {
                        ShowError("No profiles returned by API.");
                    }
                }
                else
                {
                    ShowError($"Failed to load profiles: {response.StatusCode}\n{responseContent}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error loading profiles: {ex.Message}");
            }
            finally
            {
                SpinnerOverlay.Visibility = Visibility.Collapsed;
                ProfilesLoadingBar.Visibility = Visibility.Collapsed;
                LoadProfilesButton.IsEnabled = true;
                LoadProfilesButton.Content = "Load Profiles";
            }
        }

        private async void LoadProfilesButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadProfilesAsync();
        }

        private void InputTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Skip if controls aren't initialized yet
            if (UrlTypeCombo == null)
                return;

            // Update UI based on input type
            var selectedType = (InputTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            
            if (selectedType == "web_url")
            {
                UrlTypeCombo.IsEnabled = true;
                UrlTypeCombo.SelectedIndex = 0;
            }
            else
            {
                UrlTypeCombo.IsEnabled = false;
            }
        }

        private void StartDateTime_Changed(object sender, object e)
        {
            UpdateStartAtPreview();
        }

        private void UpdateStartAtPreview()
        {
            try
            {
                // Skip if controls aren't initialized yet
                if (StartDatePicker == null || StartHourBox == null || StartMinuteBox == null || 
                    StartSecondBox == null || TimezoneBox == null || StartAtBox == null || 
                    StartAtPreviewLabel == null)
                    return;

                // Check if date is selected
                if (!StartDatePicker.SelectedDate.HasValue)
                {
                    StartAtPreviewLabel.Text = "Preview: (date not selected)";
                    StartAtBox.Text = "";
                    return;
                }

                // Parse time components
                if (!int.TryParse(StartHourBox.Text, out int hour) || hour < 0 || hour > 23)
                {
                    StartAtPreviewLabel.Text = "Preview: (invalid hour, must be 0-23)";
                    return;
                }

                if (!int.TryParse(StartMinuteBox.Text, out int minute) || minute < 0 || minute > 59)
                {
                    StartAtPreviewLabel.Text = "Preview: (invalid minute, must be 0-59)";
                    return;
                }

                if (!int.TryParse(StartSecondBox.Text, out int second) || second < 0 || second > 59)
                {
                    StartAtPreviewLabel.Text = "Preview: (invalid second, must be 0-59)";
                    return;
                }

                // Build the datetime
                var selectedDate = StartDatePicker.SelectedDate.Value;
                var dateTime = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, hour, minute, second);

                // Get timezone offset
                string timezone = TimezoneBox.Text.Trim();
                string offset = "+00:00"; // Default to UTC

                if (!string.IsNullOrWhiteSpace(timezone) && timezone != "America/Los_Angeles")
                {
                    offset = GetTimezoneOffset(timezone);
                }
                else if (timezone == "America/Los_Angeles")
                {
                    // Special handling for Los Angeles
                    offset = "-08:00"; // PST default, or -07:00 for PDT
                }

                // Format as ISO 8601 with microseconds (6 digits after decimal)
                string isoFormat = dateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff") + offset;
                StartAtBox.Text = isoFormat;
                StartAtPreviewLabel.Text = $"Preview: {isoFormat}";
            }
            catch (Exception ex)
            {
                StartAtPreviewLabel.Text = $"Preview: (error - {ex.Message})";
            }
        }

        private string GetTimezoneOffset(string timezone)
        {
            // Map common timezone names to UTC offsets
            var timezoneMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "UTC", "+00:00" },
                { "GMT", "+00:00" },
                { "EST", "-05:00" },
                { "EDT", "-04:00" },
                { "CST", "-06:00" },
                { "CDT", "-05:00" },
                { "MST", "-07:00" },
                { "MDT", "-06:00" },
                { "PST", "-08:00" },
                { "PDT", "-07:00" },
                { "America/Los_Angeles", "-08:00" },
                { "America/Denver", "-07:00" },
                { "America/Chicago", "-06:00" },
                { "America/New_York", "-05:00" },
            };

            if (timezoneMap.TryGetValue(timezone, out string? offset))
            {
                return offset;
            }

            // If it looks like an offset already (e.g., +05:30, -07:00), return it
            if (timezone.StartsWith("+") || timezone.StartsWith("-"))
            {
                return timezone;
            }

            // Try to parse as a system timezone identifier
            try
            {
                TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                DateTime now = DateTime.Now;
                TimeSpan utcOffset = tzi.GetUtcOffset(now);
                return $"{utcOffset.Hours:+00;-00}:{utcOffset.Minutes:00}";
            }
            catch
            {
                return "+00:00"; // Default to UTC if unable to parse
            }
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button during request
                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Creating Job...";
                ResponseBox.Text = "Sending request...";

                // Validate required fields
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token first");
                    return;
                }

                if (string.IsNullOrWhiteSpace(JobNameBox.Text))
                {
                    ShowError("Job Name is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(ClientTransactionIdBox.Text))
                {
                    ShowError("Client Transaction ID is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(ConnectionUrlBox.Text))
                {
                    ShowError("Connection URL is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(StartAtBox.Text))
                {
                    ShowError("Start At (ISO 8601) is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(MaxDurationBox.Text))
                {
                    ShowError("Max Duration is required");
                    return;
                }

                // Build the API URL
                string baseUrl = "https://api.verbit.co";
                string url = $"{baseUrl}/api/job/new";

                // Build request body
                var requestBody = BuildRequestBody();

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
                SubmitButton.Content = "Create Job";
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
                        ResponseBox.Text = "Bearer token generated successfully! You can now use the API features.";
                        // Auto-load profiles now that we have a bearer token
                        try
                        {
                            await LoadProfilesAsync();
                        }
                        catch
                        {
                            // ignore errors during auto-load
                        }
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

        private Dictionary<string, object> BuildRequestBody()
        {
            // Build the request body matching the new API structure:
            // {
            //   "name": "...",
            //   "client_transaction_id": "...",
            //   "input": {
            //     "language": "...",
            //     "type": "...",
            //     "url_type": "..." (if web_url),
            //     "schedule": { "start_at": "...", "max_duration": ..., "timezone": "..." },
            //     "connection_params": { "url": "..." }
            //   },
            //   "output": [{ "product": { "type": "...", "tier": "...", "target_languages": [...] } }]
            // }

            var body = new Dictionary<string, object>();

            // Add basic fields
            body["name"] = JobNameBox.Text;
            body["client_transaction_id"] = ClientTransactionIdBox.Text;
            // Add selected profile name(s) if available
            if (ProfilesCombo != null && ProfilesCombo.SelectedItem != null)
            {
                var sel = ProfilesCombo.SelectedItem.ToString();
                if (!string.IsNullOrWhiteSpace(sel))
                {
                    // single profile value (string) under key "profile"
                    body["profile"] = sel;
                }
            }

            // Build input object
            var input = new Dictionary<string, object>
            {
                ["language"] = InputLanguageBox.Text,
                ["type"] = (InputTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "web_url",
                ["service_type"] = "live"
            };

            // Add URL type if web_url
            var inputType = (InputTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (inputType == "web_url")
            {
                input["url_type"] = (UrlTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "zoom";
            }

            // Build schedule object
            var schedule = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(StartAtBox.Text))
            {
                schedule["start_at"] = StartAtBox.Text;
            }
            if (!string.IsNullOrWhiteSpace(MaxDurationBox.Text) && int.TryParse(MaxDurationBox.Text, out int maxDurationMinutes))
            {
                // Convert minutes to seconds
                schedule["max_duration"] = maxDurationMinutes * 60;
            }
            if (!string.IsNullOrWhiteSpace(TimezoneBox.Text))
            {
                schedule["timezone"] = TimezoneBox.Text;
            }
            input["schedule"] = schedule;

            // Build connection params
            var connectionParams = new Dictionary<string, object>
            {
                ["url"] = ConnectionUrlBox.Text
            };
            input["connection_params"] = connectionParams;

            body["input"] = input;

            // Build output array with product
            var product = new Dictionary<string, object>
            {
                ["type"] = (ProductTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "captions",
                ["tier"] = (TierCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "automatic",
                ["service_type"] = "live"
            };

            // Add target languages
            var targetLanguages = TargetLanguagesBox.Text.Split(',')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            product["target_languages"] = targetLanguages;

            var output = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["product"] = product }
            };
            body["output"] = output;

            return body;
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

                if (statusCode == System.Net.HttpStatusCode.Created)
                {
                    sb.AppendLine();
                    sb.AppendLine("✓ Job created successfully!");

                    var jobId = jsonObject["job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                    {
                        sb.AppendLine($"Job ID: {jobId}");
                    }

                    var warning = jsonObject["warning"]?.ToString();
                    if (!string.IsNullOrEmpty(warning))
                    {
                        sb.AppendLine($"Warning: {warning}");
                    }

                    sb.AppendLine();
                    sb.AppendLine("Next steps:");
                    sb.AppendLine("1. Upload media: POST /api/job/add_media");
                    sb.AppendLine("2. Start processing: POST /api/job/perform_transcription");
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

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all input fields
            ApiTokenBox.Clear();
            _bearerToken = string.Empty;
            TokenStatusLabel.Text = "No bearer token generated";
            TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Gray;
            
            JobNameBox.Clear();
            ClientTransactionIdBox.Clear();
            InputTypeCombo.SelectedIndex = 0;
            UrlTypeCombo.SelectedIndex = 0;
            ConnectionUrlBox.Clear();
            InputLanguageBox.Text = "en-US";
            StartDatePicker.SelectedDate = null;
            StartHourBox.Text = "12";
            StartMinuteBox.Text = "00";
            StartSecondBox.Text = "00";
            StartAtBox.Clear();
            StartAtPreviewLabel.Text = "Preview: (not set)";
            TimezoneBox.Text = "America/Los_Angeles";
            MaxDurationBox.Text = "15";
            ProductTypeCombo.SelectedIndex = 0;
            TierCombo.SelectedIndex = 0;
            TargetLanguagesBox.Text = "en-US";
            ProfilesCombo.Items.Clear();
            ProfilesCombo.SelectedIndex = -1;
            SpinnerOverlay.Visibility = Visibility.Collapsed;
            
            ResponseBox.Clear();
            LoggingBox.Clear();
            _logBuilder.Clear();
        }

        private string GetPasswordBoxText(PasswordBox passwordBox)
        {
            return passwordBox.Password;
        }

        // Menu item event handlers
        private void WebSocketOrderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Open the WebSocket Order window
            var webSocketOrderWindow = new WebSocketOrderWindow();
            webSocketOrderWindow.Show();
        }

        private void DocumentationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Open browser to API documentation
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://verbit.readme.io/reference",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open documentation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Verbit API Client v1.0\n\n" +
                "A Windows desktop application for interacting with the Verbit API.\n\n" +
                "Features:\n" +
                "• Create New Job (POST /api/job/new)\n" +
                "• Update Connection Plan (POST /api/v1/session/{order_id}/connection_plan)\n\n" +
                "For more information, visit:\nhttps://verbit.readme.io",
                "About Verbit API Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
