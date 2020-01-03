using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Samplers;
using System.Reflection;
using OpenTelemetry.Resources;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.Extensibility;
using System.IO;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Configuration;

namespace Sample.Common
{
    public static class SampleServiceCollectionExtensions
    {
        public static IServiceCollection AddWebSampleTelemetry(this IServiceCollection services, IConfiguration configuration, Action<TracerBuilder> traceBuilder = null)
        {
            if (configuration.UseOpenTelemetry())
                services.AddSampleOpenTelemetry(configuration, traceBuilder);

            if (configuration.UseApplicationInsights())
                services.AddSampleApplicationInsights(isWeb: true, configuration);

            return services;
        }

        public static IServiceCollection AddWorkerSampleTelemetry(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration.UseOpenTelemetry())
                services.AddSampleOpenTelemetry(configuration);

            if (configuration.UseApplicationInsights())
                services.AddSampleApplicationInsights(isWeb: false, configuration);

            return services;
        }


        static IServiceCollection AddSampleOpenTelemetry(this IServiceCollection services, IConfiguration configuration, Action<TracerBuilder> traceBuilder = null)
        {
            // setup open telemetry
            services.AddOpenTelemetry(builder =>
            {
                var serviceName = OpenTelemetryExtensions.TracerServiceName;

                var exporterCount = 0;

                // To start zipkin:
                // docker run -d -p 9411:9411 openzipkin/zipkin
                var zipkinUrl = configuration.GetZipkinUrl();
                if (!string.IsNullOrWhiteSpace(zipkinUrl))
                {
                    exporterCount++;

                    builder.UseZipkin(o =>
                    {
                        o.Endpoint = new Uri(zipkinUrl);
                        o.ServiceName = serviceName;
                    });
                }

                var appInsightsKey = configuration.GetApplicationInsightsInstrumentationKeyForOpenTelemetry();
                if (!string.IsNullOrWhiteSpace(appInsightsKey))
                {
                    exporterCount++;

                    builder.UseApplicationInsights(o =>
                    {
                        o.InstrumentationKey = appInsightsKey;
                        o.TelemetryInitializers.Add(new CloudRoleTelemetryInitializer());
                    });
                }

                // Running jaeger with docker
                // docker run -d--name jaeger \
                //  -e COLLECTOR_ZIPKIN_HTTP_PORT = 19411 \
                //  -p 5775:5775 / udp \
                //  -p 6831:6831 / udp \
                //  -p 6832:6832 / udp \
                //  -p 5778:5778 \
                //  -p 16686:16686 \
                //  -p 14268:14268 \
                //  -p 19411:19411 \
                //  jaegertracing/all -in-one:1.15
                var jaegerHost = configuration.GetJaegerHost();
                if (!string.IsNullOrWhiteSpace(jaegerHost))
                {
                    exporterCount++;

                    builder.UseJaeger(o =>
                    {
                        o.ServiceName = serviceName;
                        o.AgentHost = jaegerHost;
                        o.AgentPort = 6831;
                        // o.AgentPort = 14268;
                        o.MaxPacketSize = 1000;
                    });
                }

                if (exporterCount == 0)
                {
                    throw new Exception("No sink for open telemetry was configured");
                }

                builder.SetSampler(new AlwaysSampleSampler());
                builder.AddDependencyCollector(config =>
                {
                    config.SetHttpFlavor = true;
                });
                builder.AddRequestCollector(o =>
                {
                });
                builder.SetResource(new Resource(new Dictionary<string, object>
                {
                    { "service.name", serviceName }
                }));

                traceBuilder?.Invoke(builder);
            });

            if (!string.IsNullOrWhiteSpace(configuration.GetPrometheusExportURL()))
            {
                var prometheusExporterOptions = new PrometheusExporterOptions()
                {
                    Url = configuration.GetPrometheusExportURL(),
                };

                var prometheusExporter = new PrometheusExporter(prometheusExporterOptions);
                services.AddSingleton(prometheusExporter);


                // Add start/stop lifetime support
                services.AddHostedService<PromotheusExporterHostedService>();

            }

            return services;
        }

        static IServiceCollection AddSampleApplicationInsights(this IServiceCollection services, bool isWeb, IConfiguration configuration)
        {
            if (isWeb)
            {
                services.AddApplicationInsightsTelemetry(o =>
                {
                    o.InstrumentationKey = configuration.GetApplicationInsightsInstrumentationKey();
                    o.ApplicationVersion = ApplicationInformation.Version.ToString();
                });
            }
            else
            {
                services.AddApplicationInsightsTelemetryWorkerService(o =>
                {
                    o.InstrumentationKey = configuration.GetApplicationInsightsInstrumentationKey();
                    o.ApplicationVersion = ApplicationInformation.Version.ToString();
                });
            }

            services.AddSingleton<ITelemetryInitializer, CloudRoleTelemetryInitializer>();

            return services;
        }

        public static void ConfigureLogging(HostBuilderContext hostBuilderContext, ILoggingBuilder loggingBuilder)
        {
            if (hostBuilderContext.Configuration.UseApplicationInsights())
            {
                loggingBuilder.AddApplicationInsights(hostBuilderContext.Configuration.GetApplicationInsightsInstrumentationKey());
            }
        }

        public static void ConfigureAppConfiguration(IConfigurationBuilder builder, string[] args, Assembly mainAssembly)
        {
            builder.AddJsonFile("appsettings.json", optional: true);
            builder.AddEnvironmentVariables();
            //builder.AddCommandLine(args);

#if DEBUG
            // Had to add this if you use a shared file when debugging
            // It tries to get from the directory where the project is
            var path = Path.GetDirectoryName(mainAssembly.Location);
            var envJsonFile = Path.Combine(path, $"appsettings.Development.json");
            builder.AddJsonFile(envJsonFile, optional: true);
#endif
        }
    }
}
