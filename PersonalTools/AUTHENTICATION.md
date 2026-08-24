# MariaDB authentication setup

1. In HeidiSQL, open and run `PersonalTools.Database.sql` in full. It creates the `PersonalTools` database, user/session tables, and all stored procedures.
2. Configure the app's `PersonalTools` connection string. Do not commit a password to `appsettings.json`.

For local development:

```powershell
dotnet user-secrets set "ConnectionStrings:PersonalTools" "Server=YOUR_HOST;Port=3306;Database=PersonalTools;User ID=YOUR_USER;Password=YOUR_PASSWORD;SslMode=Required;"
```

For deployment, set the `ConnectionStrings__PersonalTools` environment variable to the same value. Use `SslMode=Required` when MariaDB is accessed remotely and use a least-privilege database account.

Accounts are created and maintained from the administrator-only User management page. There is no anonymous first-account setup route. Passwords are stored with PBKDF2-SHA512 and a unique random salt; sessions are stored server-side in MariaDB and represented in the browser by an encrypted, HttpOnly authentication cookie.

The login API applies a short per-IP request limit and records failed attempts against known accounts in MariaDB. Five incorrect passwords temporarily lock an account for 15 minutes. An administrator can clear that state from the User management editor without viewing or changing the account password.

Forgot-password delivery is intentionally a placeholder until an email service is configured.

## Stored procedure data access

`Data/MariaDbDataAccess.cs` is the shared database layer for this app. It exposes:

- `GetDataSP<T>` for a single mapped row;
- `GetBulkDataSP<T>` for a mapped list;
- `GetScalarSP<T>` for a single scalar result; and
- `ExecuteSP` for writes.

Feature-specific data classes should call these methods with a stored procedure name, typed `MySqlParameter` values, and a row-mapping function. The connection string is read only from runtime configuration, and the shared layer does not log it or parameter values.

## Interactive page architecture

Razor Pages provide the HTML route and view. Interactive operations use jQuery to call authenticated controllers under `/api`, which follow this flow:

`Razor Page + jQuery -> API Controller -> Funcs -> Data -> Stored Procedure`

All write requests use antiforgery validation. `Controllers/NotesController.cs` is the reference implementation for future interactive tools.

Bootstrap tables use JavaScript/jQuery to render their rows. Keep table markup to the table structure, headers, and an empty body target; use a page-specific script for the row data and rendering.

Prefer AJAX over a full-page navigation or form post for anything that doesn't need one - fetch/save data via the `/api` controllers and update the DOM in place. Reserve a real page load for the initial render and for actions that intentionally leave the page (e.g. Steam OAuth linking). This keeps interactions responsive and avoids resetting page-local UI state (open modals, scroll position, filters) on every save.
