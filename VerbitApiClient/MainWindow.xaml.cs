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

        public MainWindow()
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
                SubmitButton.Content = "Creating Job...";
                ResponseBox.Text = "Sending request...";

                // Validate required fields
                if (string.IsNullOrWhiteSpace(GetPasswordBoxText(ApiTokenBox)))
                {
                    ShowError("API Token is required");
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

                // Add authorization header
                string apiToken = GetPasswordBoxText(ApiTokenBox);
                request.Headers.Add("Authorization", $"ApiToken {apiToken}");

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

        private async void LoadProfilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate API token is provided
                if (string.IsNullOrWhiteSpace(GetPasswordBoxText(ApiTokenBox)))
                {
                    ShowError("Please enter your API Token before loading profiles");
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

                // Add authorization header
                string apiToken = GetPasswordBoxText(ApiTokenBox);
                request.Headers.Add("Authorization", $"ApiToken {apiToken}");

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
            if (!string.IsNullOrWhiteSpace(ScheduleStartAtBox.Text) ||
                !string.IsNullOrWhiteSpace(ScheduleMaxDurationBox.Text) ||
                !string.IsNullOrWhiteSpace(ScheduleTimezoneBox.Text) ||
                ScheduleRecurrenceCheckBox.IsChecked == true)
            {
                var schedule = new Dictionary<string, object>();

                if (!string.IsNullOrWhiteSpace(ScheduleStartAtBox.Text))
                {
                    schedule["start_at"] = ScheduleStartAtBox.Text;
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
            ScheduleStartAtBox.Clear();
            ScheduleMaxDurationBox.Clear();
            ScheduleTimezoneBox.Clear();
            ScheduleRecurrenceCheckBox.IsChecked = false;
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
