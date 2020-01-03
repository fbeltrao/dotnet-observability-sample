using System;
using Microsoft.Extensions.Configuration;

namespace Sample.Common
{
    public static class ConfigurationExtensions
    {
        const string RabbitMQHostNameConfigKey = "RabbitMQHostName";

        public static string GetRabbitMQHostName(this IConfiguration configuration)
        {
            var rabbitMQHostName = configuration[RabbitMQHostNameConfigKey];
            if (string.IsNullOrWhiteSpace(rabbitMQHostName))
                rabbitMQHostName = "localhost";

            return rabbitMQHostName;
        }


        public static string GetZipkinUrl(this IConfiguration configuration)
        {
            return configuration["ZIPKIN_URL"];
        }

        public static string GetJaegerHost(this IConfiguration configuration)
        {
            return configuration["JAEGER_HOST"];
        }

        public static bool UseOpenTelemetry(this IConfiguration configuration)
        {
            return configuration["USE_OPENTELEMETRY"] == "1";
        }

        public static string GetApplicationInsightsInstrumentationKeyForOpenTelemetry(this IConfiguration configuration)
        {
            return configuration["OT_APPINSIGHTS_INSTRUMENTATIONKEY"];
        }

        public static string GetApplicationInsightsInstrumentationKey(this IConfiguration configuration)
        {
            return configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
        }

        public static bool UseApplicationInsights(this IConfiguration configuration)
        {
            return configuration["USE_APPLICATIONINSIGHTS"] == "1";
        }

        public static string GetPrometheusExportURL(this IConfiguration configuration)
        {
            return configuration["PROMETHEUS_EXPORT_URL"];
        }
    }
}
