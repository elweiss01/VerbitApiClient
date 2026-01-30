using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VerbitApiClient
{
    public partial class WebSocketOrderWindow : Window
    {
        private readonly HttpClient _httpClient;
        private string _bearerToken = string.Empty;
        private List<Dictionary<string, object>> _products = new List<Dictionary<string, object>>();
        private readonly StringBuilder _logBuilder = new StringBuilder();

        public WebSocketOrderWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            
            // Wire up event handlers for start time preview
            StartDatePicker.SelectedDateChanged += (s, e) => UpdateStartAtPreview();
            StartHourBox.TextChanged += (s, e) => UpdateStartAtPreview();
            StartMinuteBox.TextChanged += (s, e) => UpdateStartAtPreview();
            StartSecondBox.TextChanged += (s, e) => UpdateStartAtPreview();
            TimezoneBox.TextChanged += (s, e) => UpdateStartAtPreview();
        }

        private void UpdateStartAtPreview()
        {
            try
            {
                if (!StartDatePicker.SelectedDate.HasValue)
                {
                    StartAtPreviewLabel.Text = "Preview: (date not selected)";
                    StartAtBox.Text = "";
                    return;
                }

                if (!int.TryParse(StartHourBox.Text, out int hour) || hour < 0 || hour > 23)
                {
                    StartAtPreviewLabel.Text = "Preview: (invalid hour, 0-23)";
                    return;
                }

                if (!int.TryParse(StartMinuteBox.Text, out int minute) || minute < 0 || minute > 59)
                {
                    StartAtPreviewLabel.Text = "Preview: (invalid minute, 0-59)";
                    return;
                }

                if (!int.TryParse(StartSecondBox.Text, out int second) || second < 0 || second > 59)
                {
                    StartAtPreviewLabel.Text = "Preview: (invalid second, 0-59)";
                    return;
                }

                var selectedDate = StartDatePicker.SelectedDate.Value;
                var dateTime = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, hour, minute, second);
                string isoFormat = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                
                StartAtBox.Text = isoFormat;
                StartAtPreviewLabel.Text = $"Preview: {isoFormat}";
            }
            catch (Exception ex)
            {
                StartAtPreviewLabel.Text = $"Preview: (error - {ex.Message})";
            }
        }

        private void GenerateTransactionIdButton_Click(object sender, RoutedEventArgs e)
        {
            ClientTransactionIdBox.Text = Guid.NewGuid().ToString();
        }

        private async void GenerateBearerTokenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "Generating...";
                }

                TokenStatusLabel.Text = "Generating bearer token...";
                TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Orange;

                string apiToken = GetPasswordBoxText(ApiTokenBox);
                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    ShowError("Please enter your API Token");
                    return;
                }

                // Build auth request
                string url = "https://users.verbit.co/api/v1/auth";
                var requestBody = new
                {
                    data = new
                    {
                        api_key = apiToken
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                string jsonContent = JsonConvert.SerializeObject(requestBody);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Log the auth request
                LogApiRequest("POST", url, jsonContent);

                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                // Log the auth response
                AddLog("=== API RESPONSE (Token Generation) ===");
                AddLog($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
                try
                {
                    var jsonObject = JToken.Parse(responseContent);
                    AddLog(jsonObject.ToString(Formatting.Indented));
                }
                catch
                {
                    AddLog(responseContent);
                }
                AddLog("");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JObject.Parse(responseContent);
                    _bearerToken = jsonResponse["token"]?.ToString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(_bearerToken))
                    {
                        TokenStatusLabel.Text = "✓ Bearer token generated successfully (valid for 24 hours)";
                        TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                        AddLog("✓✓✓ SUCCESS! Bearer token generated successfully!");
                    }
                    else
                    {
                        ShowError("Failed to extract bearer token from response");
                    }
                }
                else
                {
                    _bearerToken = string.Empty;
                    TokenStatusLabel.Text = "✗ Failed to generate bearer token";
                    TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                    ShowError($"Failed to generate bearer token: {response.StatusCode}\n{responseContent}");
                }
            }
            catch (Exception ex)
            {
                _bearerToken = string.Empty;
                TokenStatusLabel.Text = "✗ Error generating bearer token";
                TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Red;
                ShowError($"Error generating bearer token: {ex.Message}");
            }
            finally
            {
                var button = sender as Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "Generate Bearer Token";
                }
            }
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            AddProductControl();
        }

        private void AddProductControl()
        {
            var productPanel = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Background = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var innerStack = new StackPanel();

            // Header with remove button
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock 
            { 
                Text = $"Product #{ProductsStackPanel.Children.Count + 1}",
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            Grid.SetColumn(headerText, 0);

            var removeBtn = new Button
            {
                Content = "Remove",
                Width = 80,
                Height = 25,
                Background = System.Windows.Media.Brushes.LightCoral,
                Foreground = System.Windows.Media.Brushes.White
            };
            Grid.SetColumn(removeBtn, 1);

            headerGrid.Children.Add(headerText);
            headerGrid.Children.Add(removeBtn);

            innerStack.Children.Add(headerGrid);

            // Product Type
            var typeStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            typeStack.Children.Add(new TextBlock { Text = "Product Type *", FontWeight = FontWeights.Bold });
            var typeCombo = new ComboBox { Height = 30, Padding = new Thickness(8) };
            typeCombo.Items.Add(new ComboBoxItem { Content = "captions" });
            typeCombo.Items.Add(new ComboBoxItem { Content = "transcription" });
            typeCombo.Items.Add(new ComboBoxItem { Content = "translation" });
            typeCombo.Items.Add(new ComboBoxItem { Content = "addon" });
            typeCombo.SelectedIndex = 0;
            typeStack.Children.Add(typeCombo);
            innerStack.Children.Add(typeStack);

            // Product Tier
            var tierStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            tierStack.Children.Add(new TextBlock { Text = "Tier *", FontWeight = FontWeights.Bold });
            var tierCombo = new ComboBox { Height = 30, Padding = new Thickness(8) };
            tierCombo.Items.Add(new ComboBoxItem { Content = "automatic" });
            tierCombo.Items.Add(new ComboBoxItem { Content = "professional" });
            tierCombo.Items.Add(new ComboBoxItem { Content = "elite" });
            tierCombo.SelectedIndex = 0;
            tierStack.Children.Add(tierCombo);
            innerStack.Children.Add(tierStack);

            // Target Languages
            var langStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            langStack.Children.Add(new TextBlock { Text = "Target Languages (comma-separated) *", FontWeight = FontWeights.Bold });
            var langBox = new TextBox { Height = 30, Padding = new Thickness(8), Text = "en-US" };
            langStack.Children.Add(langBox);
            innerStack.Children.Add(langStack);

            productPanel.Child = innerStack;

            // Store references for removal
            removeBtn.Click += (s, e) =>
            {
                ProductsStackPanel.Children.Remove(productPanel);
            };

            ProductsStackPanel.Children.Add(productPanel);
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Creating Order...";
                ResponseBox.Text = "Sending request...";

                // Validate bearer token
                if (string.IsNullOrWhiteSpace(_bearerToken))
                {
                    ShowError("Please generate a bearer token first");
                    return;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(NameBox.Text))
                {
                    ShowError("Order Name is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(ClientTransactionIdBox.Text))
                {
                    ShowError("Client Transaction ID is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(StartAtBox.Text))
                {
                    ShowError("Start At is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(MaxDurationBox.Text) || !int.TryParse(MaxDurationBox.Text, out int _))
                {
                    ShowError("Max Duration must be a valid number");
                    return;
                }

                if (ProductsStackPanel.Children.Count == 0)
                {
                    ShowError("At least one product must be added");
                    return;
                }

                // Build request body
                var requestBody = BuildRequestBody();

                // Create HTTP request
                var url = "https://orders.verbit.co/api/v2/orders";
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                // Add authorization header with bearer token
                request.Headers.Add("Authorization", $"Bearer {_bearerToken}");

                string jsonContent = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                ResponseBox.Text = $"Request Body:\n{jsonContent}\n\nSending...";

                // Log the request
                var headers = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_bearerToken}" },
                    { "Content-Type", "application/json" }
                };
                LogApiRequest("POST", url, jsonContent, headers);

                var response = await _httpClient.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                // Log the response
                LogApiResponse(response.StatusCode, responseContent);

                DisplayResponse(response.StatusCode, responseContent, jsonContent);
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                SubmitButton.IsEnabled = true;
                SubmitButton.Content = "Create Order";
            }
        }

        private Dictionary<string, object> BuildRequestBody()
        {
            var body = new Dictionary<string, object>();

            // Basic fields
            body["name"] = NameBox.Text;
            body["client_transaction_id"] = ClientTransactionIdBox.Text;

            // Input
            var input = new Dictionary<string, object>
            {
                ["type"] = "websocket",
                ["language"] = InputLanguageBox.Text,
                ["schedule"] = new Dictionary<string, object>
                {
                    ["start_at"] = StartAtBox.Text,
                    ["max_duration"] = int.Parse(MaxDurationBox.Text) * 60, // Convert minutes to seconds
                    ["timezone"] = TimezoneBox.Text
                },
                ["connection_plan"] = new Dictionary<string, object>
                {
                    ["facility_id"] = int.Parse(FacilityIdBox.Text)
                }
            };
            body["input"] = input;

            // Output products
            var output = new List<Dictionary<string, object>>();
            foreach (var productControl in ProductsStackPanel.Children.OfType<Border>())
            {
                var innerStack = productControl.Child as StackPanel;
                if (innerStack == null) continue;

                var typeCombo = innerStack.Children.OfType<StackPanel>().FirstOrDefault()?
                    .Children.OfType<ComboBox>().FirstOrDefault();
                var tierCombo = innerStack.Children.OfType<StackPanel>().ElementAtOrDefault(1)?
                    .Children.OfType<ComboBox>().FirstOrDefault();
                var langBox = innerStack.Children.OfType<StackPanel>().ElementAtOrDefault(2)?
                    .Children.OfType<TextBox>().FirstOrDefault();

                if (typeCombo != null && tierCombo != null && langBox != null)
                {
                    var product = new Dictionary<string, object>
                    {
                        ["type"] = (typeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "captions",
                        ["tier"] = (tierCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "automatic",
                        ["target_languages"] = langBox.Text
                            .Split(',')
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList()
                    };

                    output.Add(new Dictionary<string, object>
                    {
                        ["product"] = product,
                        ["delivery"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "websocket",
                                ["format"] = "text"
                            }
                        }
                    });
                }
            }
            body["output"] = output;

            // Settings
            var settings = new Dictionary<string, object>
            {
                ["asr"] = new Dictionary<string, object>
                {
                    ["profanity_filter"] = ProfanityFilterCheckBox.IsChecked ?? false,
                    ["atmospherics"] = AtmosphericsCheckBox.IsChecked ?? false,
                    ["disfluencies_filter"] = DisfluenciesFilterCheckBox.IsChecked ?? false
                },
                ["captions_placement"] = new Dictionary<string, object>
                {
                    ["id"] = int.Parse(CaptionsPlacementIdBox.Text)
                },
                ["case_type"] = (CaseTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "upper",
                ["speaker_change_type"] = SpeakerChangeTypeBox.Text,
                ["speaker_sensitivity"] = (SpeakerSensitivityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "turn_taking_conversation",
                ["captions_stream_initial_state"] = (CaptionsStreamStateCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "block"
            };
            body["settings"] = settings;

            return body;
        }

        private void DisplayResponse(System.Net.HttpStatusCode statusCode, string content, string requestBody)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== REQUEST ===");
            sb.AppendLine(requestBody);
            sb.AppendLine("\n=== RESPONSE ===");
            sb.AppendLine($"Status Code: {(int)statusCode} {statusCode}");
            sb.AppendLine();

            try
            {
                var jsonObject = JToken.Parse(content);
                sb.AppendLine(jsonObject.ToString(Formatting.Indented));

                if (statusCode == System.Net.HttpStatusCode.Created)
                {
                    sb.AppendLine();
                    sb.AppendLine("✓ Order created successfully!");
                }
            }
            catch
            {
                sb.AppendLine(content);
            }

            ResponseBox.Text = sb.ToString();
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
                
                // Check for successful job creation
                var jobId = jsonObject["JobId"]?.ToString();
                if (!string.IsNullOrEmpty(jobId) && statusCode == System.Net.HttpStatusCode.Created)
                {
                    AddLog("");
                    AddLog("✓✓✓ SUCCESS! WebSocket order created successfully!");
                    AddLog($"Job ID: {jobId}");
                    var taskId = jsonObject["TaskId"]?.ToString();
                    if (!string.IsNullOrEmpty(taskId))
                    {
                        AddLog($"Task ID: {taskId}");
                    }
                }
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
            AddLog($"✗ ERROR: {message}");
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string GetPasswordBoxText(PasswordBox passwordBox)
        {
            return passwordBox.Password;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ApiTokenBox.Clear();
            _bearerToken = string.Empty;
            TokenStatusLabel.Text = "No bearer token generated";
            TokenStatusLabel.Foreground = System.Windows.Media.Brushes.Gray;
            
            NameBox.Clear();
            ClientTransactionIdBox.Clear();
            InputLanguageBox.Text = "en-US";
            FacilityIdBox.Clear();
            StartDatePicker.SelectedDate = null;
            StartHourBox.Text = "09";
            StartMinuteBox.Text = "40";
            StartSecondBox.Text = "00";
            StartAtBox.Clear();
            MaxDurationBox.Text = "120";
            TimezoneBox.Text = "America/Los_Angeles";
            ProfanityFilterCheckBox.IsChecked = true;
            AtmosphericsCheckBox.IsChecked = false;
            DisfluenciesFilterCheckBox.IsChecked = false;
            CaptionsPlacementIdBox.Text = "9";
            CaseTypeCombo.SelectedIndex = 0;
            SpeakerChangeTypeBox.Text = ">>";
            SpeakerSensitivityCombo.SelectedIndex = 0;
            CaptionsStreamStateCombo.SelectedIndex = 0;
            ProductsStackPanel.Children.Clear();
            ResponseBox.Clear();
            LoggingBox.Clear();
            _logBuilder.Clear();

            // Add default product
            AddProductControl();
        }

        private void ResponseTab_Click(object sender, RoutedEventArgs e)
        {
            ResponseTabContent.Visibility = Visibility.Visible;
            LoggingTabContent.Visibility = Visibility.Collapsed;
            ResponseTabButton.Background = System.Windows.Media.Brushes.LimeGreen;
            ResponseTabButton.Foreground = System.Windows.Media.Brushes.White;
            LoggingTabButton.Background = System.Windows.Media.Brushes.LightGray;
            LoggingTabButton.Foreground = System.Windows.Media.Brushes.Black;
        }

        private void LoggingTab_Click(object sender, RoutedEventArgs e)
        {
            ResponseTabContent.Visibility = Visibility.Collapsed;
            LoggingTabContent.Visibility = Visibility.Visible;
            LoggingTabButton.Background = System.Windows.Media.Brushes.LimeGreen;
            LoggingTabButton.Foreground = System.Windows.Media.Brushes.White;
            ResponseTabButton.Background = System.Windows.Media.Brushes.LightGray;
            ResponseTabButton.Foreground = System.Windows.Media.Brushes.Black;
        }
    }
}
