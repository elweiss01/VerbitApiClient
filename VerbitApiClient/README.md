# Verbit API Client - Windows Desktop App

A native Windows desktop application for interacting with the Verbit API.

## Features

- **Multiple API Endpoints**: Create new jobs and update connection plans
- Modern WPF interface with intuitive form layout and menu navigation
- Support for all API parameters (required and optional)
- Production and Sandbox environment switching (for Create Job endpoint)
- Real-time API response display with JSON formatting
- Form validation and error handling
- Secure API token input using PasswordBox
- Built-in help menu with links to API documentation

## Prerequisites

- Windows 10 or later
- .NET 8.0 SDK or later ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Verbit API token (obtain from your Verbit account)

## Building the Application

### Option 1: Using Visual Studio

1. Install Visual Studio 2022 or later with the ".NET desktop development" workload
2. Open the `VerbitApiClient` folder
3. Double-click `VerbitApiClient.csproj` to open in Visual Studio
4. Press `F5` or click "Start" to build and run

### Option 2: Using Command Line

1. Open Command Prompt or PowerShell
2. Navigate to the `VerbitApiClient` folder:
   ```
   cd C:\dev\windowsApp\VerbitApiClient
   ```
3. Build the application:
   ```
   dotnet build
   ```
4. Run the application:
   ```
   dotnet run
   ```

### Option 3: Creating an Executable

To create a standalone executable:

```bash
cd C:\dev\windowsApp\VerbitApiClient
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The executable will be located in:
```
bin\Release\net8.0-windows\win-x64\publish\VerbitApiClient.exe
```

## Using the Application

The application provides access to multiple Verbit API endpoints through a menu-driven interface. Select the desired feature from the "API Features" menu.

### Feature 1: Create New Job

Access via: **API Features > Create New Job**

Creates a new job container in the Verbit system.

#### Required Fields

1. **API Token**: Your Verbit API authentication token
2. **Profile**: The processing configuration name (e.g., "transcription", "captioning")
3. **API Version**: Currently set to 4 (default)

### Optional Fields

- **Job Name**: Human-readable identifier for the job
- **External ID**: Your reference identifier
- **Language**: RFC 5646 language code (e.g., "en-US", "es-ES")
- **Dynamic Dictionary**: Custom terminology as JSON array:
  ```json
  ["term1", "term2", "term3"]
  ```
- **Permissions**: Comma-separated access control tags
- **Labels**: Comma-separated job labels
- **Job Metadata**: Flat JSON object matching your profile schema:
  ```json
  {"field1": "value1", "field2": "value2"}
  ```

### Translation Parameters

- **Translation Profile**: Language profile for translations
- **Translation Processing Mode**: Choose "standard" or "rush"
- **Translation Languages**: Comma-separated language codes (e.g., "es-ES,fr-FR")

### Order Details

- **PO Number**: Purchase order number
- **Cost Center**: Cost center identifier

#### Environment Selection

- Check **"Use Sandbox Environment"** to test against the sandbox API
- Uncheck to use the production API

#### API Response

The application displays:
- HTTP status code
- Formatted JSON response
- Job ID (on success)
- Any warnings or error messages
- Next steps for completing the job workflow

#### Workflow Steps

After successfully creating a new job:

1. Upload media files using: `POST /api/job/add_media`
2. Initiate processing using: `POST /api/job/perform_transcription`

### Feature 2: Update Connection Plan

Access via: **API Features > Update Connection Plan**

Changes the connection plan template for an active session.

#### Required Fields

1. **Bearer Token**: Your Verbit API authentication token (Bearer format)
2. **Order ID**: The order identifier for the session
3. **Connection Plan ID**: The identifier for the connection plan template to apply

#### How to Use

1. Retrieve available connection plans using the Get Session Info endpoint
2. Enter the Order ID for the active session you want to update
3. Enter the Connection Plan ID you want to apply
4. Click "Update Plan" to submit the change

#### API Response

The application displays:
- HTTP status code
- Formatted JSON response including the updated connection plan details
- Connection Plan ID and Name (on success)
- Validation errors (if applicable)

#### Important Notes

- This endpoint uses the Realtime API base URL: `https://realtime.verbit.co`
- Requires Bearer token authentication (not ApiToken format)
- Only works with active sessions
- Connection plan must be valid for the session type

## Security Notes

- API tokens are stored in a PasswordBox (masked input)
- Tokens are only held in memory during the application session
- Use HTTPS for all API communications
- Consider using environment variables or secure storage for production deployments

## Troubleshooting

### "Profile is required" error
Make sure you've entered a valid processing configuration name in the Profile field.

### Invalid JSON format errors
When entering JSON data (Dynamic Dictionary, Job Metadata), ensure it's valid JSON format:
- Dynamic Dictionary: Array format `["item1", "item2"]`
- Job Metadata: Object format `{"key": "value"}`

### Network errors
- Verify your internet connection
- Check that you're using the correct environment (Sandbox vs Production)
- Ensure your API token is valid and has appropriate permissions

### Build errors
- Verify .NET 8.0 SDK is installed: `dotnet --version`
- Restore NuGet packages: `dotnet restore`

## API Documentation

For complete API documentation, visit:
- Create New Job: https://verbit.readme.io/reference/post_job-new
- Update Connection Plan: https://verbit.readme.io/reference/change_cpt_api_v1_session__order_id__connection_plan_post
- All APIs: https://verbit.readme.io/reference

## Dependencies

- .NET 8.0
- Newtonsoft.Json 13.0.3

## Project Structure

```
VerbitApiClient/
├── App.xaml                    # Application definition
├── App.xaml.cs                 # Application code-behind
├── MainWindow.xaml             # Main window UI for Create Job
├── MainWindow.xaml.cs          # Main window logic with navigation menu
├── ConnectionPlanWindow.xaml   # Connection plan window UI
├── ConnectionPlanWindow.xaml.cs # Connection plan API integration
├── VerbitApiClient.csproj      # Project file
└── README.md                   # This file
```

## License

This is a sample application for demonstration purposes.
