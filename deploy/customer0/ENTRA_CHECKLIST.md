# Customer 0 Entra Checklist

Use this checklist when creating the real Customer 0 login configuration for CMIForge.

## App Registration

1. Open Microsoft Entra ID in the Azure portal.
2. Go to **App registrations**.
3. Create a new registration:
   - Name: `CMIForge Customer 0`
   - Supported account types: **Accounts in this organizational directory only**
   - Redirect URI platform: **Web**
   - Redirect URI: `https://cmiforge-customer0-web.azurewebsites.net/signin-oidc`
4. After creation, copy:
   - Application client ID
   - Directory tenant ID
5. Go to **Certificates & secrets**.
6. Create a client secret and copy the secret value immediately.

## Authentication Settings

The Customer 0 App Service needs these settings:

```text
Authentication__Microsoft__Enabled=true
Authentication__Microsoft__TenantId=<directory tenant id>
Authentication__Microsoft__ClientId=<application client id>
Authentication__Microsoft__ClientSecret=<client secret value>
Authentication__Microsoft__CallbackPath=/signin-oidc
CMIForge__BootstrapAdminEmail=<your Entra sign-in email>
CMIForge__DemoMode=false
CMIForge__DemoResetEnabled=false
```

## Optional In-App Entra User Creation

CMIForge can create an Entra login when an administrator creates a CMIForge user. This is separate from the user's work/contact email.

The login is the user's UPN. It can look like `first.last@cmiforge.com` without requiring a real mailbox for that address.

Before turning this on:

1. Confirm `cmiforge.com` is still a verified custom domain in Microsoft Entra.
2. Grant the Customer 0 web app managed identity Microsoft Graph application permission `User.ReadWrite.All`.
3. Configure the App Service settings below.

Current `cmiforge.com` verification record kept in Azure DNS:

```text
Type: TXT
Name: @
Value: MS=ms36377677
TTL: 3600
```

If verification ever needs to be rerun:

```powershell
az rest --method POST --uri "https://graph.microsoft.com/v1.0/domains/cmiforge.com/verify"
```

Enable provisioning only after verification and Graph permissions are confirmed:

```text
EntraProvisioning__Enabled=true
EntraProvisioning__Domain=cmiforge.com
```

When provisioning is enabled, **Entities > Users > Create** exposes the Entra login option, creates the Entra user through Microsoft Graph, stores the Entra object ID on the CMIForge user, and displays the temporary password once.

## Optional Graph Mail Access

CMIForge also has an outbound notification and inbound email intake direction that should use Microsoft Graph rather than tenant-supplied SMTP.

Before turning on live workflow notifications or inbound email intake for Customer 0:

1. Confirm the shared mailbox or mailbox anchor exists, such as `intake@cmiforge.com` or `notifications@cmiforge.com`.
2. Confirm the tenant alias exists, such as `customer0@cmiforge.com`.
3. Enable Exchange send-from-alias behavior if aliases need to appear as real outbound sender identities.
4. Grant the Customer 0 app identity the minimum Graph mail permissions needed for the configured mailbox.
5. Scope Graph mail access to the shared mailbox with an Exchange application access policy where possible.
6. Configure CMIForge email settings for outbound from/reply-to and inbound allowed sender domains.
7. Keep inbound processing disabled until allowed sender domains and mailbox permissions are verified.

The V1 inbound subject format is:

```text
Client: Acme Corp
Client: Acme Corp; Matter:
Client: Acme Corp; Matter: Lease Review
```

`Client:` is required. `Matter:` can be omitted or blank, in which case the app creates a reviewable client-only intake.

## Redirect URIs

Start with the Azure-generated hostname:

```text
https://cmiforge-customer0-web.azurewebsites.net/signin-oidc
```

The custom app domain redirect URI is also configured:

```text
https://app.cmiforge.com/signin-oidc
```

Do not remove the Azure hostname redirect until the custom domain has been tested.

## First Sign-In Smoke Test

1. Browse to the Customer 0 app.
2. Confirm it redirects to Microsoft sign-in.
3. Sign in with the bootstrap admin email.
4. Confirm the app opens without the public demo banner.
5. Open **Entities > Users**.
6. Confirm the signed-in user exists and is active.
7. Open **System > Security**.
8. Confirm the signed-in user has administrator access.

## Deployment And Smoke Note

After Entra/auth changes, deploy Customer 0 unless Gabe explicitly says to hold deployment. Smoke-check both the anonymous `/Signup` route and the authenticated root route. The expected root behavior for an unauthenticated browser is a redirect to Microsoft sign-in, while `/Signup` should remain publicly reachable.

## Notes

- Entra handles password resets, MFA, account verification, and conditional access.
- CMIForge handles app permissions after the user is authenticated.
- Store the client secret as an App Service setting for now; move it to Key Vault before serious production use.
- Rotate the client secret before handing a tenant to an external customer.
