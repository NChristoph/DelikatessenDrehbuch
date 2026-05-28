# Security Setup Guide - DelikatessenDrehbuch

## Overview

This document explains how to configure sensitive credentials for the DelikatessenDrehbuch application. All sensitive data has been removed from source control and must be configured locally using **User Secrets** for development and **Environment Variables** for production.

---

## Development Setup (Local)

### 1. User Secrets Configuration

The application uses .NET User Secrets for local development. Navigate to the project directory and run the following commands:

```bash
cd "C:\Users\Free8\source\repos\DelikatessenDrehbuch\DelikatessenDrehbuch"

# Database Connection String
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=YOUR_DATABASE;Persist Security Info=False;User ID=YOUR_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Bunny.net Storage
dotnet user-secrets set "Bunny_Net_Passwort_Lager" "YOUR_BUNNY_STORAGE_PASSWORD"

# Bunny.net Stream API
dotnet user-secrets set "Bunny_Net_Api_Stream" "YOUR_BUNNY_STREAM_API_KEY"

# Email Settings
dotnet user-secrets set "EmailSettings:Password" "YOUR_EMAIL_APP_PASSWORD"

# WorldMiniApp SuperUser Hash
dotnet user-secrets set "WorldMiniApp:SuperUserHash" "YOUR_SUPERUSER_HASH"
```

### 2. Verify Configuration

Check that your secrets are configured correctly:

```bash
dotnet user-secrets list
```

### 3. Run the Application

```bash
dotnet run
```

---

## Production Setup (Azure App Service)

### Environment Variables

Configure the following **Application Settings** in Azure App Service:

| Variable Name | Description | Example Value |
|--------------|-------------|---------------|
| `AZURE_SQL_CONNECTIONSTRING` | Full SQL connection string | `Server=tcp:...;Password=...;` |
| `BUNNY_STORAGE_PASSWORD` | Bunny.net Storage API Key | `84124467-...` |
| `BUNNY_STREAM_API_KEY` | Bunny.net Stream API Key | `49035e33-...` |
| `EMAIL_PASSWORD` | Gmail App Password | `hhpg xpwz ltng lvrp` |
| `WORLDMINIAPP_SUPERUSER_HASH` | Admin Hash for WorldMiniApp | `0x2da33d4d7...` |

### Azure Portal Configuration

1. Navigate to your App Service in Azure Portal
2. Go to **Configuration** → **Application Settings**
3. Add each environment variable with the corresponding value
4. Click **Save** and **Restart** the App Service

---

## Security Best Practices

### ✅ Do:
- Use User Secrets for local development
- Use Environment Variables for production
- Keep `appsettings.json` free of sensitive data
- Regularly rotate credentials

### ❌ Don't:
- Never commit secrets to Git
- Don't share User Secrets files
- Don't hardcode credentials in source code
- Don't use Development secrets in Production

---

## Troubleshooting

### Application won't start locally

1. Verify User Secrets are configured:
   ```bash
   dotnet user-secrets list
   ```

2. Check connection string format:
   - Must include `Password=...`
   - No line breaks or extra spaces

3. Test database connectivity:
   ```bash
   sqlcmd -S YOUR_SERVER.database.windows.net -U YOUR_USER -P YOUR_PASSWORD -d YOUR_DATABASE
   ```

### Admin access not working (WorldMiniApp)

1. Verify SuperUserHash is configured:
   ```bash
   dotnet user-secrets list | grep SuperUserHash
   ```

2. Check that the hash matches your Worldcoin user hash
3. Ensure the hash starts with `0x` (hexadecimal format)

### Bunny.net uploads failing

1. Verify both Bunny credentials are set:
   - `Bunny_Net_Passwort_Lager` (Storage)
   - `Bunny_Net_Api_Stream` (Stream)

2. Test API connectivity:
   ```bash
   curl -H "AccessKey: YOUR_STORAGE_PASSWORD" https://storage.bunnycdn.com/avocadon
   ```

### Email notifications not sending

1. Use a Gmail App Password (not your regular password)
2. Enable "Less secure app access" in Google Account settings
3. Verify SMTP settings in `appsettings.json`:
   - Server: `smtp.gmail.com`
   - Port: `587`
   - SSL: `true`

---

## Project Structure

```
DelikatessenDrehbuch/
├── appsettings.json              # Base configuration (no secrets)
├── appsettings.Development.json  # Development overrides (no secrets)
├── Program.cs                    # Environment variable override logic
├── Areas/WorldMiniApp/
│   └── Controllers/
│       ├── WorldMiniAppBaseController.cs  # SuperUserHash from Configuration
│       ├── FeedController.cs              # SuperUserHash from Configuration
│       └── AdminAdsController.cs          # Null-safe admin check
└── .gitignore                    # Excludes secrets.json, *.env
```

---

## Migration Notes

### Breaking Change for Team Members

After pulling the latest code, all developers must configure User Secrets locally:

1. Pull the latest code
2. Run the User Secrets commands above
3. Restart the application

**Without User Secrets, the application will fail to start with a database connection error.**

---

## Contact

For questions or issues with credentials, contact the project administrator.

**Never share credentials via Slack, email, or other insecure channels.**
