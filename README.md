# Crestron Blazor Identity with SQLite

## Project Overview
This is a Blazor web application using Radzen Blazor components, implementing user identity with SQLite as the database backend. Designed for Crestron control systems, this is a proof-of-concept implementation.

## Prerequisites
- .NET SDK
- Crestron development environment

## Getting Started

### Publishing for Linux ARM
To publish the application for a Linux ARM target, use the following command:

```bash
dotnet publish -c Release -r linux-arm --self-contained false \
  -p:CopyLocalLockFileAssemblies=true \
  -p:PublishTrimmed=false
```

### Native SQLite Library
This project uses SQLite as its database. When deploying to Linux ARM:
- Ensure the appropriate native SQLite library is available
- The publish command will copy necessary library dependencies

### Application Behavior
- Database File: `app.db` will be created in `/user/app.db` on first run
- Listening Port: 7070
- HTTPS: Not implemented in this version (tested in other projects)

## Deployment Notes
- Copy the CPZ from the `/bin/release/linux-arm` directory

## Known Issues
- Reconnection prompts during authentication actions
- UI is a basic proof-of-concept implementation

## Components
- Radzen Blazor Components
- ASP.NET Core Identity
- SQLite Entity Framework Core

## Troubleshooting
If you encounter the SQLite migration error:
- Manually delete existing `app.db` if needed
