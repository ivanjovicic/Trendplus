# Production environment variables for Trendplus Core API

# RabbitMQ (CloudAMQP with SSL) - cow.rmq2.cloudamqp.com
$env:RabbitMq__HostName = "cow.rmq2.cloudamqp.com"
$env:RabbitMq__Port = "5671"
$env:RabbitMq__UserName = "jsvhgrbd"
$env:RabbitMq__Password = "IO1FsweLq_abLDVKNebQwjZVE6WXsVrV"
$env:RabbitMq__VirtualHost = "jsvhgrbd"
$env:RabbitMq__ExchangeName = "trendplus.events"
$env:RabbitMq__ExchangeType = "topic"
$env:RabbitMq__UseSsl = "true"
$env:RabbitMq__Enabled = "true"

# Database
$env:ConnectionStrings__DefaultConnection = "Host=your-postgres;Port=5432;Database=trendplus;Username=dbuser;Password=dbpass;SSL Mode=Require;"
$env:ConnectionStrings__AnalyticsConnection = "Host=your-postgres;Port=5432;Database=trendplus_analytics;Username=dbuser;Password=dbpass;SSL Mode=Require;"

# ASP.NET Core
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://0.0.0.0:8080"

Write-Host "? Production environment variables loaded for CloudAMQP (SSL enabled on port 5671)" -ForegroundColor Green
