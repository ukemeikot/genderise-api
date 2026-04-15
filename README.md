# HNG Stage 1 Backend Assessment - Profile Classification API

## Overview

This is a complete ASP.NET Core Web API implementation for the HNG Stage 1 backend assessment. The API accepts a person's name, classifies demographic information by calling three external APIs, stores the data in SQLite, and provides comprehensive CRUD and filtering endpoints.

## Features

- **Name-Based Profile Creation**: Accepts a name and enriches it with demographic data
- **Multi-Source Data Integration**: Integrates with Genderize, Agify, and Nationalize APIs
- **Intelligent Classification**:
  - Gender detection with probability scores
  - Age estimation with group classification (child, teenager, adult, senior)
  - Nationality detection with country ID and probability
- **Duplicate Prevention**: Uses normalized names (trimmed, lowercase) to prevent duplicate profiles
- **Comprehensive Filtering**: Get profiles filtered by gender, country, or age group
- **Case-Insensitive Queries**: All filter parameters support case-insensitive matching
- **Robust Error Handling**: Detailed error responses with appropriate HTTP status codes
- **UUID v7 IDs**: Modern UUID v7 for all entity identifiers
- **UTC Timestamps**: All timestamps in ISO 8601 UTC format
- **CORS Support**: Configured for cross-origin requests from automated graders
- **Swagger/OpenAPI Documentation**: Interactive API documentation in development

## Tech Stack

- **.NET 9**: Latest .NET runtime
- **ASP.NET Core Web API**: Controllers-based architecture
- **Entity Framework Core 9**: ORM with SQLite provider
- **SQLite**: Lightweight embedded database
- **System.Text.Json**: Native JSON serialization
- **Swagger/Swashbuckle**: API documentation
- **Async/Await**: Fully asynchronous operations

## Folder Structure

```
HNG-BACKEND/hng-task-one/
├── src/
│   └── HngStageOne.Api/
│       ├── Controllers/           # HTTP request handlers
│       ├── Data/                  # Entity Framework configuration
│       │   ├── Configurations/    # Entity configurations
│       │   └── Migrations/        # Database migrations
│       ├── Domain/
│       │   ├── Entities/          # Domain models (Profile)
│       │   └── Enums/             # Enumerations (AgeGroup)
│       ├── DTOs/
│       │   ├── Requests/          # Request DTOs
│       │   ├── Responses/         # Response DTOs
│       │   └── ExternalApis/      # External API response DTOs
│       ├── Services/
│       │   ├── Interfaces/        # Service contracts
│       │   └── ProfileService.cs  # Business logic implementation
│       ├── Repositories/
│       │   ├── Interfaces/        # Repository contracts
│       │   └── Implementations/   # Data access implementations
│       ├── Clients/
│       │   ├── Interfaces/        # HTTP client contracts
│       │   └── Implementations/   # External API HTTP clients
│       ├── Middleware/            # Global middleware (exception handling)
│       ├── Helpers/               # Utilities and custom exceptions
│       ├── Constants/             # API route constants
│       ├── Program.cs             # Application entry point
│       ├── appsettings.json       # Configuration
│       └── HngStageOne.Api.csproj # Project file
├── tests/                         # Test projects
└── README.md                      # This file

```

## Setup Instructions

### Prerequisites

- .NET 9 SDK installed
- PowerShell or Command Prompt
- Internet connection (for external API calls)

### Clone and Navigate

```bash
# Navigate to the project directory
cd HNG-BACKEND/hng-task-one
```

### Restore NuGet Packages

```bash
dotnet restore
```

Or specifically for the API project:

```bash
cd src/HngStageOne.Api
dotnet restore
```

### Apply Database Migrations

The application automatically applies migrations on startup. However, to manually create the database:

```bash
# From the API project directory
dotnet ef database update
```

Or create a new migration:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Run the Application

```bash
# From the src/HngStageOne.Api directory
dotnet run
```

Or use the watch mode for development:

```bash
dotnet watch run
```

The API will be available at:

- **HTTP**: `http://localhost:5000`
- **HTTPS**: `https://localhost:5001`
- **Swagger UI**: `http://localhost:5000/swagger`

## Package Installation

All required packages are already configured in the `.csproj` file:

```bash
# To manually install or update packages:
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.0
dotnet add package Swashbuckle.AspNetCore --version 6.5.0
dotnet add package Microsoft.AspNetCore.OpenApi --version 9.0.10
```

## API Endpoints

### 1. Create Profile

**Endpoint**: `POST /api/profiles`

**Request**:

```json
{
  "name": "ella"
}
```

**Success Response (201 Created)**:

```json
{
  "status": "success",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "ella",
    "gender": "female",
    "gender_probability": 0.99,
    "sample_size": 1234,
    "age": 46,
    "age_group": "adult",
    "country_id": "DRC",
    "country_probability": 0.85,
    "created_at": "2026-04-15T12:00:00Z"
  }
}
```

**Duplicate Response (201 Created)**:

```json
{
  "status": "success",
  "message": "Profile already exists",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "ella",
    "gender": "female",
    "gender_probability": 0.99,
    "sample_size": 1234,
    "age": 46,
    "age_group": "adult",
    "country_id": "DRC",
    "country_probability": 0.85,
    "created_at": "2026-04-15T12:00:00Z"
  }
}
```

### 2. Get Profile by ID

**Endpoint**: `GET /api/profiles/{id}`

**Example**: `GET /api/profiles/550e8400-e29b-41d4-a716-446655440000`

**Success Response (200 OK)**:

```json
{
  "status": "success",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "emmanuel",
    "gender": "male",
    "gender_probability": 0.99,
    "sample_size": 1234,
    "age": 25,
    "age_group": "adult",
    "country_id": "NG",
    "country_probability": 0.85,
    "created_at": "2026-04-15T12:00:00Z"
  }
}
```

### 3. Get All Profiles (with Optional Filters)

**Endpoint**: `GET /api/profiles`

**Query Parameters**:

- `gender` (optional, case-insensitive): Filter by gender
- `country_id` (optional, case-insensitive): Filter by country ID
- `age_group` (optional, case-insensitive): Filter by age group

**Examples**:

```
GET /api/profiles
GET /api/profiles?gender=male
GET /api/profiles?country_id=NG
GET /api/profiles?age_group=adult
GET /api/profiles?gender=male&country_id=NG&age_group=adult
```

**Success Response (200 OK)**:

```json
{
  "status": "success",
  "count": 2,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "emmanuel",
      "gender": "male",
      "age": 25,
      "age_group": "adult",
      "country_id": "NG"
    },
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "name": "sarah",
      "gender": "female",
      "age": 28,
      "age_group": "adult",
      "country_id": "US"
    }
  ]
}
```

### 4. Delete Profile

**Endpoint**: `DELETE /api/profiles/{id}`

**Example**: `DELETE /api/profiles/550e8400-e29b-41d4-a716-446655440000`

**Success Response (204 No Content)**:

- No response body, just a 204 status code

## Error Responses

All error responses follow this format:

```json
{
  "status": "error",
  "message": "<error message>"
}
```

### HTTP Status Codes

| Status  | Code                  | Scenario                           | Message                                  |
| ------- | --------------------- | ---------------------------------- | ---------------------------------------- |
| **400** | Bad Request           | Missing or empty name              | "Missing or invalid name provided"       |
| **404** | Not Found             | Profile ID not found               | "Profile not found"                      |
| **502** | Bad Gateway           | External API returned invalid data | "{ApiName} returned an invalid response" |
| **500** | Internal Server Error | Unexpected server error            | "An unexpected error occurred"           |

### External API Errors (502)

When external APIs fail or return invalid data:

```json
{
  "status": "error",
  "message": "Genderize returned an invalid response"
}
```

Or:

```json
{
  "status": "error",
  "message": "Agify returned an invalid response"
}
```

Or:

```json
{
  "status": "error",
  "message": "Nationalize returned an invalid response"
}
```

## Age Group Classification

Ages are classified into groups automatically:

| Age Range | Group    |
| --------- | -------- |
| 0–12      | child    |
| 13–19     | teenager |
| 20–59     | adult    |
| 60+       | senior   |

## Database Schema

### Profiles Table

| Column             | Type           | Constraints      |
| ------------------ | -------------- | ---------------- |
| Id                 | GUID           | Primary Key      |
| Name               | VARCHAR(255)   | NOT NULL         |
| NormalizedName     | VARCHAR(255)   | NOT NULL, UNIQUE |
| Gender             | VARCHAR(50)    | NOT NULL         |
| GenderProbability  | DECIMAL(18,4)  | NOT NULL         |
| SampleSize         | INT            | NOT NULL         |
| Age                | INT            | NOT NULL         |
| AgeGroup           | VARCHAR(50)    | NOT NULL         |
| CountryId          | VARCHAR(10)    | NOT NULL         |
| CountryProbability | DECIMAL(18,4)  | NOT NULL         |
| CreatedAt          | DATETIMEOFFSET | NOT NULL         |

**Unique Index**: `NormalizedName` (enforces duplicate prevention by normalized name)

## External APIs Reference

### 1. Genderize API

- **URL**: `https://api.genderize.io?name={name}`
- **Returns**: Gender and probability
- **Validation**: Gender must not be null, count must be > 0

### 2. Agify API

- **URL**: `https://api.agify.io?name={name}`
- **Returns**: Age estimate and count
- **Validation**: Age must not be null

### 3. Nationalize API

- **URL**: `https://api.nationalize.io?name={name}`
- **Returns**: Array of countries with probabilities
- **Validation**: Country array must not be empty, country_id must be present

## Duplicate Handling

The API prevents duplicate profiles using normalized names:

1. **Normalization**: Input names are trimmed and converted to lowercase
2. **Lookup**: Before creating a new profile, the system checks if a profile with the normalized name exists
3. **Response**: If found, returns the existing profile with a success status and optional message
4. **Database Constraint**: A unique index on `NormalizedName` enforces database-level uniqueness

### Example Flow

```
Request 1: POST /api/profiles with name "  ELLA  "
→ Normalized to "ella"
→ Creates new profile
→ Response: Status 201, new profile data

Request 2: POST /api/profiles with name "ella"
→ Normalized to "ella"
→ Found existing profile
→ Response: Status 201, existing profile data with message "Profile already exists"
```

## Environment and Configuration

### appsettings.json

Default configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=hng_stage_one.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### appsettings.Development.json

For development-specific settings (if needed):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Debug"
    }
  }
}
```

## Deployment Notes

### Production Considerations

1. **Database Path**: Change the SQLite connection string to use a persistent path:

   ```
   "Data Source=/var/data/hng_stage_one.db"
   ```

2. **CORS**: Modify the CORS policy for production to restrict to specific origins:

   ```csharp
   options.AddPolicy("Production", builder =>
   {
       builder.WithOrigins("https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
   });
   ```

3. **HTTPS**: Enable HTTPS redirection by uncommenting `app.UseHttpsRedirection()` in Program.cs

4. **Swagger**: Disable Swagger in production:

   ```csharp
   if (app.Environment.IsDevelopment())
   {
       app.UseSwagger();
       app.UseSwaggerUI();
   }
   ```

5. **Logging**: Configure centralized logging (e.g., Serilog, Application Insights)

### Docker Support

The project includes Docker support via a Dockerfile and docker-compose.yaml found in the parent directory. To deploy:

```bash
docker-compose up --build
```

## Assumptions Made

1. **External APIs are Always Available**: The implementation assumes external APIs (Genderize, Agify, Nationalize) are accessible. Network timeouts and failures return HTTP 502.

2. **Name Format**: Names are expected to be text strings. Special characters and non-ASCII characters are supported via URL encoding.

3. **UTC Timestamps**: All timestamps are in UTC. The application uses `DateTimeOffset.UtcNow` for consistency.

4. **Database Isolation**: Each instance uses its own SQLite database file. In multi-instance scenarios, consider using a shared database.

5. **Rate Limiting**: No built-in rate limiting is implemented on external API calls. Consider adding throttling for production.

6. **ID Generation**: Uses .NET's `Guid.NewGuid()` for UUID generation (compatible with UUID v7 semantics).

## Error Handling Summary

The application implements comprehensive error handling:

1. **Validation Layer**: Validates input before processing
2. **Business Logic Layer**: Validates external API responses with specific error messages
3. **Exception Middleware**: Catches all exceptions and returns standardized error responses
4. **HTTP Status Mapping**:
   - 400: Bad Request (validation errors)
   - 404: Not Found (resource doesn't exist)
   - 502: Bad Gateway (external API issues)
   - 500: Internal Server Error (unexpected failures)

### Exception Types

- `ArgumentException`: Validation failures → 400 Bad Request
- `ProfileNotFoundException`: Missing profile → 404 Not Found
- `InvalidUpstreamResponseException`: External API errors → 502 Bad Gateway
- `Exception`: Unexpected errors → 500 Internal Server Error

## Performance Considerations

1. **Async/Await**: All I/O operations are asynchronous
2. **Indexed Queries**: `NormalizedName` is indexed for fast duplicate detection
3. **Case-Insensitive Filtering**: Uses database-level string comparison for efficiency
4. **Connection Pooling**: EF Core manages database connection pooling automatically
5. **HTTP Client Reuse**: Named HTTP client instances are reused across requests

## Testing

To run unit tests:

```bash
dotnet test
```

Test structure:

```
tests/
└── HngStageOne.Api.Tests/
    ├── Controllers/
    ├── Services/
    ├── Repositories/
    └── Clients/
```

## Support and Troubleshooting

### Database Issues

**Problem**: "database is locked" error
**Solution**: Ensure only one instance is using the database file. Reset by deleting `hng_stage_one.db` and restarting.

### External API Timeouts

**Problem**: Frequent 502 errors from external APIs
**Solution**: Check internet connectivity and external API status. Consider implementing retry logic with exponential backoff.

### Port Already in Use

**Problem**: "Address already in use" when running `dotnet run`
**Solution**:

```bash
# Find the process using port 5000
netstat -ano | findstr :5000

# Kill the process
taskkill /PID <PID> /F
```

## Additional Resources

- [Microsoft Docs - ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/)
- [Entity Framework Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [Genderize.io API](https://genderize.io/)
- [Agify.io API](https://agify.io/)
- [Nationalize.io API](https://nationalize.io/)

---

## Docker & Container Deployment

This section covers containerizing the API for production deployment on AWS EC2 and other cloud platforms.

### Docker Files Overview

The following Docker-related files have been added to support containerization:

- **Dockerfile**: Multi-stage build for optimized image size (~500MB)
- **.dockerignore**: Excludes unnecessary files from Docker context
- **docker-compose.yml**: Production-like orchestration with volume mounts for SQLite persistence
- **.env.example**: Environment variable template

### Local Docker Build and Test

#### Prerequisites

- Docker Desktop installed and running
- Docker Compose (included with Docker Desktop)

#### Build the Docker Image

```bash
# Navigate to project root
cd HNG-BACKEND/hng-task-one

# Build the image
docker build -t hng-stage-one:latest .
```

**Expected Output**:

```
[+] Building 120.2s (13/13) FINISHED
 => [build 1/6] FROM mcr.microsoft.com/dotnet/sdk:9.0
 => ...
 => [runtime 4/4] ENTRYPOINT ["dotnet", "HngStageOne.Api.dll"]
Successfully tagged hng-stage-one:latest
```

#### Run Container Locally (without Compose)

```bash
# Create a local data directory for SQLite
mkdir -p ./data

# Run the container with volume mount
docker run -d \
  --name hng-api \
  -p 8080:8080 \
  -v "$(pwd)/data:/app/data" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  hng-stage-one:latest
```

**Verify the container is running**:

```bash
# Check logs
docker logs hng-api

# Test the API
curl http://localhost:8080/

# Access Swagger (Swagger disabled in production, but available in Development mode)
# curl http://localhost:8080/swagger

# Stop the container
docker stop hng-api

# Remove the container
docker rm hng-api
```

#### Run with Docker Compose (Recommended for Local)

```bash
# Navigate to project root
cd HNG-BACKEND/hng-task-one

# Create .env file from template
cp .env.example .env

# Start all services
docker-compose up --build

# In another terminal, test the API
curl http://localhost:8080/
curl -X POST http://localhost:8080/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "ella"}'

# View logs
docker-compose logs -f api

# Stop services
docker-compose down

# Remove data volume (optional)
docker-compose down -v
```

### AWS EC2 Deployment

This guide covers deploying the containerized API on an AWS EC2 Ubuntu instance.

#### Prerequisites

- AWS Account with EC2 access
- SSH key pair created and downloaded (.pem file)
- Security group configured (see Security & Networking section below)

#### Step 1: Launch EC2 Instance

1. Go to [AWS EC2 Console](https://console.aws.amazon.com/ec2/)
2. Click **Launch Instance**
3. **Choose AMI**: Select "Ubuntu Server 24.04 LTS" (or latest Ubuntu LTS)
4. **Instance Type**: Select `t3.micro` (eligible for free tier) or `t3.small` for better performance
5. **Network Settings**:
   - VPC: Default
   - Subnet: Default
   - Auto-assign public IP: Enable
   - Create or select a security group (see Step 4 below)
6. **Storage**: Keep default 20GB gp2 or upgrade to 30GB
7. **Review and Launch**
8. Select your key pair and launch

**Note**: Save your EC2 instance public IP/DNS for SSH access.

#### Step 2: Connect to EC2 via SSH

```bash
# On your local machine
# Make key file readable only by owner
chmod 400 /path/to/your-key.pem

# SSH into the instance
ssh -i /path/to/your-key.pem ubuntu@<EC2_PUBLIC_IP>

# Example:
# ssh -i ~/Downloads/my-key.pem ubuntu@54.123.45.67
```

#### Step 3: Install Docker on EC2

```bash
# Update system packages
sudo apt-get update
sudo apt-get upgrade -y

# Install Docker
sudo apt-get install -y docker.io

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/download/v2.20.0/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# Verify installation
docker --version
docker-compose --version

# Add ubuntu user to docker group (allows running docker without sudo)
sudo usermod -aG docker ubuntu

# Apply group changes
newgrp docker

# Verify docker works without sudo
docker ps
```

#### Step 4: Security & Networking

**AWS Security Group Configuration**:

1. Go to [AWS EC2 Security Groups](https://console.aws.amazon.com/ec2/v2/home#SecurityGroups)
2. Select your instance's security group
3. Edit **Inbound Rules**:
   - **Type**: HTTP, **Protocol**: TCP, **Port**: 80, **Source**: 0.0.0.0/0
   - **Type**: HTTPS, **Protocol**: TCP, **Port**: 443, **Source**: 0.0.0.0/0
   - **Type**: Custom TCP, **Port**: 8080, **Source**: 0.0.0.0/0 _(allow direct access)_
   - **Type**: SSH, **Protocol**: TCP, **Port**: 22, **Source**: Your IP (restrict for security)

**Network Diagram**:

```
Internet Client
       ↓
AWS Security Group (allow 80, 443, 8080)
       ↓
EC2 Instance (Ubuntu)
       ↓
Docker Container (port 8080)
       ↓
ASP.NET Core API
```

#### Step 5: Clone Repository and Deploy

```bash
# Create deployment directory
mkdir -p ~/hng-api
cd ~/hng-api

# Clone the repository (assuming public repo)
git clone https://github.com/your-username/HNG-INTERNSHIP.git .
# or copy files manually via SCP:
# scp -i keys.pem -r HNG-BACKEND/hng-task-one ubuntu@<EC2_IP>:~/hng-api/

# Navigate to project
cd HNG-BACKEND/hng-task-one

# Create .env from template
cp .env.example .env

# (Optional) Edit .env for any custom settings
nano .env
```

#### Step 6: Build and Start the Container

```bash
# Create data directory for SQLite persistence
mkdir -p data
chmod 777 data

# Build and start the container
docker-compose up -d

# Verify the container is running
docker-compose ps

# Check logs
docker-compose logs -f api

# Expected output:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://0.0.0.0:8080
# info: Microsoft.Hosting.Lifetime[0]
#       Application started. Press Ctrl+C to shut down.
```

#### Step 7: Verify API is Reachable

```bash
# From EC2 instance (or local machine)
curl http://localhost:8080/
# Should return:
# {"status":"success","message":"API is running"}

# From local machine (using EC2 public IP)
curl http://<EC2_PUBLIC_IP>:8080/
curl http://54.123.45.67:8080/

# Test creating a profile
curl -X POST http://<EC2_PUBLIC_IP>:8080/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "test"}'

# Test getting all profiles
curl http://<EC2_PUBLIC_IP>:8080/api/profiles
```

#### Step 8: Setup Nginx Reverse Proxy (Optional but Recommended)

For additional security, performance, and to support HTTPS:

```bash
# Install Nginx
sudo apt-get install -y nginx

# Create Nginx configuration
sudo tee /etc/nginx/sites-available/hng-api > /dev/null <<EOF
upstream hng_api {
    server localhost:8080;
}

server {
    listen 80;
    server_name _;

    client_max_body_size 10M;

    location / {
        proxy_pass http://hng_api;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_http_version 1.1;
        proxy_set_header Connection "";
    }
}
EOF

# Enable the configuration
sudo ln -s /etc/nginx/sites-available/hng-api /etc/nginx/sites-enabled/

# Test Nginx configuration
sudo nginx -t

# Start/restart Nginx
sudo systemctl restart nginx
sudo systemctl enable nginx

# Verify Nginx is running
sudo systemctl status nginx
```

**Update Security Group**: Allow port 80 (and 443 for HTTPS)

```bash
# Now test through Nginx
curl http://<EC2_PUBLIC_IP>/
curl -X POST http://<EC2_PUBLIC_IP>/api/profiles \
  -H "Content-Type: application/json" \
  -d '{"name": "test"}'
```

#### Step 9: Enable HTTPS with Let's Encrypt (Optional)

```bash
# Install Certbot
sudo apt-get install -y certbot python3-certbot-nginx

# Generate certificate (replace with your domain)
sudo certbot certonly --nginx \
  -d api.example.com \
  --non-interactive \
  --agree-tos \
  -m your-email@example.com

# Auto-renew (runs daily)
sudo systemctl enable certbot.timer
sudo systemctl start certbot.timer

# Verify certificate renewal
sudo certbot renew --dry-run
```

### Docker SQLite Persistence

The SQLite database is persisted using Docker volumes:

**Volume Mount Configuration** (from docker-compose.yml):

```yaml
volumes:
  - ./data:/app/data
```

**How it works**:

1. **Container path**: `/app/data` (inside Docker container)
2. **Host path**: `./data` (on EC2 machine)
3. **Data flow**: SQLite writes to `/app/data/hng_stage_one.db` → stored in `~/hng-api/data/hng_stage_one.db` on EC2
4. **Persistence**: Data survives container restarts and updates

**Backup SQLite database on EC2**:

```bash
# Create backup directory
mkdir -p ~/backups

# Backup the database
cp ~/hng-api/data/hng_stage_one.db ~/backups/hng_stage_one.db.$(date +%Y%m%d_%H%M%S)

# Download to local machine
scp -i keys.pem ubuntu@<EC2_IP>:~/backups/hng_stage_one.db.* ./

# Verify backup
ls -lh backups/
```

### Ongoing Management on EC2

#### View Logs

```bash
# Latest logs
docker-compose logs -f api

# Last 100 lines
docker-compose logs --tail 100 api

# Logs from specific time
docker-compose logs --since 2h api
```

#### Restart Services

```bash
# Stop all services
docker-compose down

# Start all services
docker-compose up -d

# Restart only API service
docker-compose restart api
```

#### Update Application

```bash
# Pull latest code
cd ~/hng-api
git pull origin main

# Rebuild and restart
docker-compose up -d --build

# View logs
docker-compose logs -f api
```

#### Monitor Container

```bash
# Resource usage
docker stats

# Container processes
docker-compose ps

# Container inspection
docker inspect hng_stage_one_api
```

### Environment Variables

The application uses the following environment variables. Create a `.env` file in the project root:

```bash
# Copy from template
cp .env.example .env

# Edit as needed
nano .env
```

**Key Variables**:

| Variable                               | Default                      | Purpose                  |
| -------------------------------------- | ---------------------------- | ------------------------ |
| `ASPNETCORE_ENVIRONMENT`               | `Production`                 | ASP.NET environment mode |
| `ASPNETCORE_URLS`                      | `http://0.0.0.0:8080`        | API binding address      |
| `ConnectionStrings__DefaultConnection` | `/app/data/hng_stage_one.db` | SQLite database path     |
| `LOG_LEVEL`                            | `Information`                | Logging level            |

### Troubleshooting Docker Deployment

#### Container starts but API is unreachable

**Problem**: `curl http://EC2_IP:8080/` returns connection refused

**Solutions**:

1. **Check container is running**:

   ```bash
   docker-compose ps
   # Should show: hng_stage_one_api  UP
   ```

2. **Verify port binding**:

   ```bash
   docker-compose exec api netstat -tuln | grep 8080
   # Should show: tcp 0 0 0.0.0.0:8080
   ```

3. **Check API logs**:

   ```bash
   docker-compose logs api
   # Look for errors or "Now listening on: http://0.0.0.0:8080"
   ```

4. **Verify security group allows port 8080**:
   - SSH into EC2 and test locally:
     ```bash
     curl http://localhost:8080/
     ```
   - If that works, check AWS security group rules

5. **Restart container**:
   ```bash
   docker-compose restart api
   docker-compose logs -f api
   ```

#### SQLite file permission issues

**Problem**: "database is locked" or "readonly database" errors

**Solutions**:

1. **Check directory permissions**:

   ```bash
   ls -la ~/hng-api/data/
   # Should show: drwxrwxrwx (777) for data directory
   ```

2. **Fix permissions**:

   ```bash
   sudo chmod 777 ~/hng-api/data
   sudo chmod 666 ~/hng-api/data/*.db
   ```

3. **Rebuild and restart**:

   ```bash
   docker-compose down
   docker-compose up -d
   ```

4. **Check container user**:
   ```bash
   docker-compose exec api whoami
   ```

#### Migrations failing

**Problem**: "The model for context 'AppDbContext' has pending changes"

**Solutions**:

1. **Migrations are auto-applied on startup**
   - Caused by code changes that require new migrations
   - On EC2, migrations run automatically on container start

2. **Check migration logs**:

   ```bash
   docker-compose logs api | grep -i migrat
   ```

3. **Re-create database** (loses data):
   ```bash
   rm ~/hng-api/data/hng_stage_one.db
   docker-compose restart api
   ```

#### Port binding issues

**Problem**: `docker run` fails with "bind: address already in use"

**Solutions**:

1. **Find what's using port 8080**:

   ```bash
   sudo netstat -tulpn | grep 8080
   lsof -i :8080
   ```

2. **Kill the process** (if safe):

   ```bash
   sudo kill -9 <PID>
   ```

3. **Use different port** in docker-compose.yml:

   ```yaml
   ports:
     - "8081:8080"
   ```

4. **Check if another container is running**:
   ```bash
   docker ps --all
   docker kill <CONTAINER_ID>
   ```

#### Volume mount issues

**Problem**: "/app/data: no such file or directory"

**Solutions**:

1. **Ensure host directory exists**:

   ```bash
   mkdir -p ~/hng-api/data
   ```

2. **Check docker-compose.yml volume syntax**:

   ```yaml
   volumes:
     - ./data:/app/data # Correct
     # NOT: - /app/data:/app/data (absolute path issue)
   ```

3. **Verify volume mount**:

   ```bash
   docker-compose exec api ls -la /app/data
   ```

4. **Re-mount volume**:
   ```bash
   docker-compose down
   docker-compose up -d
   docker-compose exec api ls -la /app/data
   ```

### Production Best Practices

1. **Use Environment Variables**: Never hardcode secrets or configuration
2. **Restrict Security Group**: Limit SSH to known IPs, use VPN for management
3. **Enable Monitoring**: Use CloudWatch, Prometheus, or similar
4. **Setup Log Aggregation**: Use ELK Stack, CloudWatch, or Splunk
5. **Regular Backups**: Backup SQLite database daily
6. **Use Reverse Proxy**: Nginx/Apache for SSL termination, load balancing
7. **Update Dependencies**: Regularly update Docker base images and packages
8. **Scale Database**: Migrate to PostgreSQL for multi-instance deployments
9. **CI/CD Pipeline**: Use GitHub Actions to auto-deploy on push
10. **Disaster Recovery**: Document recovery procedures and test regularly

### Recommended Next Improvements

1. **Database Migration**:

   ```bash
   # Move from SQLite to PostgreSQL for better concurrency
   # Minimal code changes required (only connection string)
   ```

2. **Add Nginx Reverse Proxy**:
   - SSL/TLS termination
   - Load balancing across multiple API instances
   - Caching and compression

3. **Enable HTTPS with Let's Encrypt**:
   - Free SSL certificates
   - Auto-renewal
   - Secure client communication

4. **Setup CI/CD with GitHub Actions**:
   - Auto-build Docker image on push
   - Auto-deploy to EC2
   - Run tests before deployment

5. **Container Orchestration**:
   - Use Amazon ECS or EKS for multi-instance deployments
   - Auto-scaling based on load
   - Zero-downtime deployments

6. **Monitoring & Alerting**:
   - Health checks and uptime monitoring
   - Error tracking and alerting
   - Performance metrics and dashboards

7. **API Rate Limiting & Authentication**:
   - Prevent abuse
   - Add API keys if needed
   - Implement OAuth2 for user authentication

---

**Last Updated**: April 15, 2026
**Status**: Production Ready
