$apiToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ0b2tlbiI6ImdBQUFBQUJwZkxLLVRzNVNqQkNwdEJPU3hGVGhQSHVFTzBaVG5IR1doR05TcXNNLVdfaFpMNGk2MTFpUEl0cEFnSXFVazhCbFFkaWthMUtrZUJ2TzlUX3c3bDFzaDAtYzcwS1BzNFdmcWZjVjJ1ay14S2tOZVh6UmJtaUdRcWszRmVtQkRvekpzSlNFSFBDQkd5WkJabjBiTW50TUdQTi1qYmJmYlI5MEQ2YmgzaHVOZDdub3ZZb190a2loQTJFdVVuT1czdDZWOVBhYzRIdlRrcUtPaVc0aHRNNEtyVmlVdjVueG4tTHFUa2V6X0ptbkFTaE1OS2M9IiwiaWF0IjoxNzY5Nzc5OTAyfQ.J6wjCIsigR-QQT_hdhoNhnGx7YlCKsU4Z-7stflkRMF8NLGzVxW7R7NGKaXd7De_pC3iw1q7Zoash2GYTRCkiJICHRXBklRgYJ69Py2LlFsjG7OS4cGuqi6u5ZiJkFxrY55VHOWzzs4zE6Mv4Ypr3_sq0QT-YdWNZDfsIxDfnKKOeHAiMcg0lkeWeltGgdwvOVX9Adjh_0TtYg2ZXB3VGzHFppyfBUD0x6fucQWC9UFQXLxRigGixyZlIQwCMez2N299bBFkC7UBebV-Ca1XPbXv5-dybfYADF2Flh1lbmEpjmqkdmxizNZIMXoVkj1WduF5G70M2c29pzyIN1Zr0A"

# Step 1: Generate bearer token
Write-Host "Step 1: Generating bearer token..."
$authUrl = "https://users.verbit.co/api/v1/auth"
$authBody = @{
    data = @{
        api_key = $apiToken
    }
} | ConvertTo-Json

try {
    $authResponse = Invoke-RestMethod -Uri $authUrl -Method Post -Body $authBody -ContentType "application/json" -ErrorAction Stop
    $bearerToken = $authResponse.token
    Write-Host "✓ Bearer token generated"
} catch {
    Write-Host "✗ Failed to generate bearer token: $_"
    exit 1
}

# Step 2: Load profiles
Write-Host "Step 2: Loading profiles..."
$profilesUrl = "https://api.verbit.co/api/profiles?v=4"
$headers = @{
    "Authorization" = "Bearer $bearerToken"
}

try {
    $profilesResponse = Invoke-RestMethod -Uri $profilesUrl -Method Get -Headers $headers -ErrorAction Stop
    $profiles = $profilesResponse.profiles
    Write-Host "✓ Loaded $($profiles.Count) profile(s)"
    if ($profiles.Count -gt 0) {
        $selectedProfile = $profiles[0].name
        Write-Host "  Profile: $selectedProfile"
    } else {
        Write-Host "✗ No profiles available"
        exit 1
    }
} catch {
    Write-Host "✗ Failed to load profiles: $_"
    exit 1
}

# Step 3: Create a test job
Write-Host "Step 3: Creating test job..."
$jobUrl = "https://api.verbit.co/api/job/new"
$now = [DateTime]::UtcNow
$startAt = $now.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss.ffffff") + "-08:00"

$jobBody = @{
    name = "Test Job $(Get-Date -Format 'HHmmss')"
    client_transaction_id = [guid]::NewGuid().ToString()
    profile = $selectedProfile
    input = @{
        language = "en-US"
        type = "web_url"
        url_type = "zoom"
        service_type = "live"
        schedule = @{
            start_at = $startAt
            max_duration = 900
            timezone = "America/Los_Angeles"
        }
        connection_params = @{
            url = "https://zoom.us/meeting/123456789"
        }
    }
    output = @(
        @{
            product = @{
                type = "captions"
                tier = "automatic"
                service_type = "live"
                target_languages = @("en-US")
            }
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "`nRequest JSON:"
Write-Host $jobBody

try {
    $jobResponse = Invoke-RestMethod -Uri $jobUrl -Method Post -Headers $headers -Body $jobBody -ContentType "application/json" -ErrorAction Stop
    Write-Host "`n✓ Job created successfully!"
    Write-Host "Job ID: $($jobResponse.job_id)"
    Write-Host "`nResponse:"
    Write-Host ($jobResponse | ConvertTo-Json -Depth 10)
} catch {
    Write-Host "`n✗ Failed to create job"
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "`nResponse body: $responseBody"
    }
}
