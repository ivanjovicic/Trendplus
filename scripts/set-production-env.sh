#!/bin/bash
# Production environment variables for Trendplus Core API

# RabbitMQ (CloudAMQP with SSL) - cow.rmq2.cloudamqp.com
export RabbitMq__HostName="cow.rmq2.cloudamqp.com"
export RabbitMq__Port="5671"
export RabbitMq__UserName="jsvhgrbd"
export RabbitMq__Password="IO1FsweLq_abLDVKNebQwjZVE6WXsVrV"
export RabbitMq__VirtualHost="jsvhgrbd"
export RabbitMq__ExchangeName="trendplus.events"
export RabbitMq__ExchangeType="topic"
export RabbitMq__UseSsl="true"
export RabbitMq__Enabled="true"

# Database
export ConnectionStrings__DefaultConnection="Host=your-postgres;Port=5432;Database=trendplus;Username=dbuser;Password=dbpass;SSL Mode=Require;"
export ConnectionStrings__AnalyticsConnection="Host=your-postgres;Port=5432;Database=trendplus_analytics;Username=dbuser;Password=dbpass;SSL Mode=Require;"

# ASP.NET Core
export ASPNETCORE_ENVIRONMENT="Production"
export ASPNETCORE_URLS="http://0.0.0.0:8080"

echo "? Production environment variables loaded for CloudAMQP (SSL enabled on port 5671)"
