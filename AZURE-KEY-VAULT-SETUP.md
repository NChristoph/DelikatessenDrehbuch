# Azure Key Vault Setup für Admin-Secrets

## Warum Key Vault?
- Zentrale Secret-Verwaltung
- Automatische Rotation
- Audit-Logging
- RBAC-Zugriffskontrolle
- Verschlüsselt im Transit und At-Rest

## Setup-Schritte

### 1. Key Vault erstellen (einmalig)

```bash
# Key Vault erstellen
az keyvault create \
  --name delikatessendrehbuch-kv \
  --resource-group IhrResourceGroupName \
  --location westeurope

# Secrets hinzufügen
az keyvault secret set \
  --vault-name delikatessendrehbuch-kv \
  --name WorldMiniApp--AdminPassword \
  --value '$2a$11$deinHash...'

az keyvault secret set \
  --vault-name delikatessendrehbuch-kv \
  --name WorldMiniApp--SuperUserHash \
  --value '0x2da33d4d7152caf4dad616bffa6fed2a7fd896ebe32be8806c79ed5010ff4839'
```

### 2. Managed Identity für App Service aktivieren

```bash
# System-assigned Managed Identity aktivieren
az webapp identity assign \
  --resource-group IhrResourceGroupName \
  --name IhrAppServiceName

# Principal ID speichern (wird ausgegeben)
# z.B.: "principalId": "12345678-1234-1234-1234-123456789abc"
```

### 3. App Service Zugriff auf Key Vault geben

```bash
# Zugriff gewähren (ersetzen Sie <principal-id> mit der ID aus Schritt 2)
az keyvault set-policy \
  --name delikatessendrehbuch-kv \
  --object-id <principal-id> \
  --secret-permissions get list
```

### 4. App Service Configuration anpassen

Im Azure Portal → App Service → Configuration:

```
Name:  KeyVaultName
Value: delikatessendrehbuch-kv
```

### 5. Code anpassen (in Program.cs)

Fügen Sie nach `var builder = WebApplication.CreateBuilder(args);` hinzu:

```csharp
// Azure Key Vault Integration (nur wenn Key Vault konfiguriert ist)
var keyVaultName = builder.Configuration["KeyVaultName"];
if (!string.IsNullOrEmpty(keyVaultName))
{
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}
```

NuGet-Package erforderlich:
```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

## Vorteile

✅ Secrets nie im Code oder Config-Files
✅ Automatische Authentifizierung via Managed Identity
✅ Zentrale Verwaltung aller Secrets
✅ Audit-Log für jeden Zugriff
✅ Automatische Rotation möglich

## Kosten

~€0.03 pro 10.000 Operationen
~€0.023 pro Secret/Monat

Für diesen Use-Case: < €1/Monat
