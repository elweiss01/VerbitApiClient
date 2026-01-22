using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VerbitApiClient
{
    public class Profile
    {
        public string Name { get; set; } = string.Empty;
        public int Turnaround { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Turnaround}h)";
        }
    }

    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private string _bearerToken = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            
            // Setup date/time picker events to update preview
            ScheduleStartDatePicker.SelectedDateChanged += ScheduleDateTime_Changed;
            ScheduleStartHourBox.TextChanged += ScheduleDateTime_Changed;
            ScheduleStartMinuteBox.TextChanged += ScheduleDateTime_Changed;
            ScheduleStartSecondBox.TextChanged += ScheduleDateTime_Changed;
            ScheduleTimezoneBox.TextChanged += ScheduleDateTime_Changed;
        }

        private void ScheduleDateTime_Changed(object? sender, EventArgs e)
        {
            UpdateSchedulePreview();
        }

        private void UpdateSchedulePreview()
        {
            try
            {
                if (ScheduleStartDatePicker.SelectedDate == null)
                {
                    ScheduleStartAtPreviewLabel.Text = "Preview: (not set)";
                    return;
                }

                if (!int.TryParse(ScheduleStartHourBox.Text, out int hour) || hour < 0 || hour > 23)
                {
                    ScheduleStartAtPreviewLabel.Text = "Preview: (invalid hour)";
                    return;
                }

                if (!int.TryParse(ScheduleStartMinuteBox.Text, out int minute) || minute < 0 || minute > 59)
                {
                    ScheduleStartAtPreviewLabel.Text = "Preview: (invalid minute)";
                    return;
                }

                if (!int.TryParse(ScheduleStartSecondBox.Text, out int second) || second < 0 || second > 59)
                {
                    ScheduleStartAtPreviewLabel.Text = "Preview: (invalid second)";
                    return;
                }

                var selectedDate = ScheduleStartDatePicker.SelectedDate.Value;
                var dateTime = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, hour, minute, second);

                // Get timezone offset
                string timezone = ScheduleTimezoneBox.Text.Trim();
                string offset = "+00:00"; // Default to UTC

                if (!string.IsNullOrWhiteSpace(timezone))
                {
                    offset = GetTimezoneOffset(timezone);
                }

                // Format as ISO 8601 with milliseconds
                string isoFormat = dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + offset;
                ScheduleStartAtPreviewLabel.Text = $"Preview: {isoFormat}";
            }
            catch
            {
                ScheduleStartAtPreviewLabel.Text = "Preview: (invalid input)";
            }
        }

        private string GetTimezoneOffset(string timezone)
        {
            // Map common timezone abbreviations to UTC offsets
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
                { "BST", "+01:00" },
                { "IST", "+05:30" },
                { "AEST", "+10:00" },
                { "AEDT", "+11:00" },
                { "JST", "+09:00" },
                { "CET", "+01:00" },
                { "CEST", "+02:00" },
            };

            if (timezoneMap.TryGetValue(timezone, out string? offset))
            {
                return offset;
            }

            // If it looks like an offset already (e.g., +05:30), return it
            if (timezone.StartsWith("+") || timezone.StartsWith("-"))
            {
                return timezone;
            }

            // Try to parse as a timezone identifier
            try
            {
                TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                DateTime now = DateTime.Now;
                TimeSpan utcOffset = tzi.GetUtcOffset(now);
                return utcOffset.ToString(@"hh\:mm");
            }
            catch
            {
                // Default to UTC if unable to parse
                return "+00:00";
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

                // Validate bearer token is generated
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token first");
                    return;
                }

                if (ProfileBox.SelectedItem == null)
                {
                    ShowError("Profile is required. Please load and select a profile.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(VersionBox.Text))
                {
                    ShowError("API Version is required");
                    return;
                }

                // Build the API URL
                string baseUrl = UseSandboxCheckBox.IsChecked == true
                    ? "https://sandbox-api.verbit.co"
                    : "https://api.verbit.co";
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

        private async void LoadProfilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate bearer token is generated
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token before loading profiles");
                    return;
                }

                // Disable button during request
                LoadProfilesButton.IsEnabled = false;
                LoadProfilesButton.Content = "Loading...";
                ProfileBox.Items.Clear();

                // Build the API URL - profiles endpoint is only on production
                string url = "https://api.verbit.co/api/profiles?v=4";

                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add authorization header with bearer token
                request.Headers.Add("Authorization", $"Bearer {_bearerToken}");

                // Send request
                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    var jsonResponse = JObject.Parse(responseContent);
                    var profiles = jsonResponse["profiles"]?.ToObject<List<Profile>>();

                    if (profiles != null && profiles.Count > 0)
                    {
                        // Populate the ComboBox
                        foreach (var profile in profiles)
                        {
                            ProfileBox.Items.Add(profile);
                        }

                        // Select the first item by default
                        ProfileBox.SelectedIndex = 0;

                        ResponseBox.Text = $"Successfully loaded {profiles.Count} profile(s)";
                    }
                    else
                    {
                        ShowError("No profiles found for this API token");
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
                // Re-enable button
                LoadProfilesButton.IsEnabled = true;
                LoadProfilesButton.Content = "Load Profiles";
            }
        }

        private Dictionary<string, object> BuildRequestBody()
        {
            var selectedProfile = ProfileBox.SelectedItem as Profile;
            var body = new Dictionary<string, object>
            {
                ["profile"] = selectedProfile?.Name ?? string.Empty,
                ["v"] = int.Parse(VersionBox.Text)
            };

            // Add optional string parameters
            AddIfNotEmpty(body, "job_name", JobNameBox.Text);
            AddIfNotEmpty(body, "external_id", ExternalIdBox.Text);
            AddIfNotEmpty(body, "language", LanguageBox.Text);
            AddIfNotEmpty(body, "customer_id", CustomerIdBox.Text);

            // Add dynamic dictionary (JSON array)
            if (!string.IsNullOrWhiteSpace(DynamicDictionaryBox.Text))
            {
                try
                {
                    var dictionary = JsonConvert.DeserializeObject<List<string>>(DynamicDictionaryBox.Text);
                    if (dictionary != null && dictionary.Count > 0)
                    {
                        body["dynamic_dictionary"] = dictionary;
                    }
                }
                catch
                {
                    throw new Exception("Invalid JSON format for Dynamic Dictionary. Expected array of strings.");
                }
            }

            // Add permissions (comma-separated to array)
            if (!string.IsNullOrWhiteSpace(PermissionsBox.Text))
            {
                var permissions = PermissionsBox.Text.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
                if (permissions.Count > 0)
                {
                    body["permissions"] = permissions;
                }
            }

            // Add labels (comma-separated to array)
            if (!string.IsNullOrWhiteSpace(LabelsBox.Text))
            {
                var labels = LabelsBox.Text.Split(',')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
                if (labels.Count > 0)
                {
                    body["labels"] = labels;
                }
            }

            // Add job metadata (JSON object)
            if (!string.IsNullOrWhiteSpace(JobMetadataBox.Text))
            {
                try
                {
                    var metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(JobMetadataBox.Text);
                    if (metadata != null && metadata.Count > 0)
                    {
                        body["job_metadata"] = metadata;
                    }
                }
                catch
                {
                    throw new Exception("Invalid JSON format for Job Metadata. Expected flat JSON object.");
                }
            }

            // Add translation parameters
            AddIfNotEmpty(body, "translation_profile", TranslationProfileBox.Text);

            var selectedMode = (TranslationModeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (!string.IsNullOrWhiteSpace(selectedMode))
            {
                body["translation_processing_mode"] = selectedMode;
            }

            // Add translation languages (comma-separated to array)
            if (!string.IsNullOrWhiteSpace(TranslationLanguagesBox.Text))
            {
                var languages = TranslationLanguagesBox.Text.Split(',')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
                if (languages.Count > 0)
                {
                    body["translation_languages"] = languages;
                }
            }

            // Add order details
            if (!string.IsNullOrWhiteSpace(PoNumberBox.Text) || !string.IsNullOrWhiteSpace(CostCenterBox.Text))
            {
                var orderDetails = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(PoNumberBox.Text))
                {
                    orderDetails["po_number"] = PoNumberBox.Text;
                }
                if (!string.IsNullOrWhiteSpace(CostCenterBox.Text))
                {
                    orderDetails["cost_center"] = CostCenterBox.Text;
                }
                if (orderDetails.Count > 0)
                {
                    body["order_details"] = orderDetails;
                }
            }

            // Add schedule
            if (ScheduleStartDatePicker.SelectedDate.HasValue ||
                !string.IsNullOrWhiteSpace(ScheduleMaxDurationBox.Text) ||
                !string.IsNullOrWhiteSpace(ScheduleTimezoneBox.Text) ||
                ScheduleRecurrenceCheckBox.IsChecked == true)
            {
                var schedule = new Dictionary<string, object>();

                if (ScheduleStartDatePicker.SelectedDate.HasValue)
                {
                    // Build ISO 8601 datetime from picker values
                    if (!int.TryParse(ScheduleStartHourBox.Text, out int hour) || hour < 0 || hour > 23)
                    {
                        throw new Exception("Start Hour must be a valid hour (0-23)");
                    }
                    if (!int.TryParse(ScheduleStartMinuteBox.Text, out int minute) || minute < 0 || minute > 59)
                    {
                        throw new Exception("Start Minute must be a valid minute (0-59)");
                    }
                    if (!int.TryParse(ScheduleStartSecondBox.Text, out int second) || second < 0 || second > 59)
                    {
                        throw new Exception("Start Second must be a valid second (0-59)");
                    }

                    var selectedDate = ScheduleStartDatePicker.SelectedDate.Value;
                    var dateTime = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, hour, minute, second);

                    // Get timezone offset
                    string timezone = ScheduleTimezoneBox.Text.Trim();
                    string offset = "+00:00"; // Default to UTC

                    if (!string.IsNullOrWhiteSpace(timezone))
                    {
                        offset = GetTimezoneOffset(timezone);
                    }

                    // Format as ISO 8601 with milliseconds
                    string isoFormat = dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff") + offset;
                    schedule["start_at"] = isoFormat;
                }

                if (!string.IsNullOrWhiteSpace(ScheduleMaxDurationBox.Text))
                {
                    if (int.TryParse(ScheduleMaxDurationBox.Text, out int maxDurationMinutes))
                    {
                        // Convert minutes to seconds
                        schedule["max_duration"] = maxDurationMinutes * 60;
                    }
                    else
                    {
                        throw new Exception("Max Duration must be a valid number (minutes).");
                    }
                }

                if (!string.IsNullOrWhiteSpace(ScheduleTimezoneBox.Text))
                {
                    schedule["timezone"] = ScheduleTimezoneBox.Text;
                }

                if (ScheduleRecurrenceCheckBox.IsChecked == true)
                {
                    schedule["recurrence"] = true;
                }

                if (schedule.Count > 0)
                {
                    body["schedule"] = schedule;
                }
            }

            return body;
        }

        private void AddIfNotEmpty(Dictionary<string, object> dictionary, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                dictionary[key] = value;
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

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all input fields
            ApiTokenBox.Clear();
            _bearerToken = string.Empty;
            TokenStatusLabel.Text = "No bearer token generated";
            TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Gray;
            CustomerIdBox.Clear();
            ProfileBox.Items.Clear();
            ProfileBox.SelectedIndex = -1;
            VersionBox.Text = "4";
            JobNameBox.Clear();
            ExternalIdBox.Clear();
            LanguageBox.Clear();
            DynamicDictionaryBox.Clear();
            PermissionsBox.Clear();
            LabelsBox.Clear();
            JobMetadataBox.Clear();
            TranslationProfileBox.Clear();
            TranslationModeCombo.SelectedIndex = 0;
            TranslationLanguagesBox.Clear();
            PoNumberBox.Clear();
            CostCenterBox.Clear();
            ScheduleStartDatePicker.SelectedDate = null;
            ScheduleStartHourBox.Text = "00";
            ScheduleStartMinuteBox.Text = "00";
            ScheduleStartSecondBox.Text = "00";
            ScheduleMaxDurationBox.Clear();
            ScheduleTimezoneBox.Clear();
            ScheduleRecurrenceCheckBox.IsChecked = false;
            ScheduleStartAtPreviewLabel.Text = "Preview: (not set)";
            ResponseBox.Clear();
            UseSandboxCheckBox.IsChecked = false;
        }

        private string GetPasswordBoxText(PasswordBox passwordBox)
        {
            return passwordBox.Password;
        }

        // Menu item event handlers
        private void CreateJobMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Already on this page, do nothing or refresh
            MessageBox.Show("You are already on the Create New Job page.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ConnectionPlanMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Open the Connection Plan window
            var connectionPlanWindow = new ConnectionPlanWindow();
            connectionPlanWindow.Show();
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
