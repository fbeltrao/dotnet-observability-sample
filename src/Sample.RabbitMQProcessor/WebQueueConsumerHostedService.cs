using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;
using Sample.Common;
using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;

namespace Sample.RabbitMQProcessor
{
    public class WebQueueConsumerHostedService : IHostedService
    {
        private string rabbitMQHostName;
        private IConnection connection;
        private IModel channel;
        private AsyncEventingBasicConsumer consumer;

        private string timeApiURL;
        private readonly ILogger logger;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly Tracer tracer;
        private readonly TelemetryClient telemetryClient;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public WebQueueConsumerHostedService(IOptions<SampleAppOptions> sampleAppOptions, ILogger<WebQueueConsumerHostedService> logger, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
        {
            // To start RabbitMQ on docker:
            // docker run -d --hostname -rabbit --name test-rabbit -p 15672:15672 -p 5672:5672 rabbitmq:3-management
            this.rabbitMQHostName = sampleAppOptions.Value.RabbitMQHostName;

            this.timeApiURL = sampleAppOptions.Value.TimeAPIUrl;
            this.logger = logger;
            this.httpClientFactory = httpClientFactory;
            var tracerFactory = serviceProvider.GetService<TracerFactoryBase>();
            this.tracer = tracerFactory?.GetApplicationTracer();
            this.telemetryClient = serviceProvider.GetService<TelemetryClient>();
            this.jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var factory = new ConnectionFactory() { HostName = rabbitMQHostName, DispatchConsumersAsync = true };
                    this.connection = factory.CreateConnection();
                    this.channel = connection.CreateModel();

                    channel.QueueDeclare(queue: Constants.WebQueueName, exclusive: false);

                    this.consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += ProcessWebQueueMessageAsync;
                    channel.BasicConsume(queue: Constants.WebQueueName,
                                            autoAck: true,
                                            consumer: consumer);

                    logger.LogInformation("RabbitMQ consumer started");
                    return;
                }
                catch (BrokerUnreachableException ex)
                {
                    logger.LogError(ex, "Failed to connect to RabbitMQ. Trying again in 3 seconds");

                    if (this.consumer != null && this.channel != null)
                    {
                        this.channel.BasicCancel(this.consumer.ConsumerTag);                        
                    }

                    this.channel?.Dispose();

                    this.connection?.Dispose();


                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    }
                }                
            }
        }

        private async Task ProcessWebQueueMessageAsync(object sender, BasicDeliverEventArgs @event)
        {
            //var correlationId = @event.BasicProperties.CorrelationId;
            TraceParent traceParent = null;
            if (@event.BasicProperties.Headers.TryGetValue(TraceParent.HeaderKey, out var rawTraceParent) && rawTraceParent is byte[] binRawTraceParent)
            {
                traceParent = TraceParent.CreateFromString(Encoding.UTF8.GetString(binRawTraceParent));
            }
            
            ISpan span = null;
            IOperationHolder<RequestTelemetry> operation = null;

            try
            {
                if (traceParent == null)
                    throw new Exception("Trace information not found in message");

                if (tracer != null)
                {
                    var traceId = ActivityTraceId.CreateFromString(traceParent.TraceId);
                    var parentId = ActivitySpanId.CreateFromString(traceParent.SpanId);
                    var parentContext = new SpanContext(traceId, parentId, ActivityTraceFlags.Recorded, isRemote: true);
                    tracer.StartActiveSpan("Process single RabbitMQ message", parentContext, SpanKind.Consumer, out span);
                    span.SetAttribute("queue", Constants.WebQueueName);
                }

                using (operation = telemetryClient?.StartOperation<RequestTelemetry>("Process single RabbitMQ message", traceParent.TraceId, traceParent.SpanId))
                {
                    if (operation != null)
                    {
                        operation.Telemetry.Properties.Add("queue", Constants.WebQueueName);
                    }

                    using (logger.BeginScope("processing message {correlationId}", traceParent.TraceId))
                    {
                        var apiFullUrl = $"{timeApiURL}/api/time/dbtime";
                        var time = await httpClientFactory.CreateClient().GetStringAsync(apiFullUrl);

                        // Get the payload
                        var message = JsonSerializer.Deserialize<EnqueuedMessage>(@event.Body, jsonSerializerOptions);
                        if (!string.IsNullOrEmpty(message.EventName))
                        {
                            span?.AddEvent(message.EventName);
                            telemetryClient?.TrackEvent(message.EventName);
                        }

                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug("Processed message: {message}", Encoding.UTF8.GetString(@event.Body));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (span != null)
                {
                    span.Status = Status.Internal.WithDescription(ex.ToString());
                }

                if (operation != null)
                {
                    operation.Telemetry.Success = false;
                    telemetryClient.TrackException(ex);
                }
            }
            finally
            {
                span?.End();
            }
        }
    

        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.channel.BasicCancel(this.consumer.ConsumerTag);
            this.channel.Close();
            this.connection.Close();

            return Task.CompletedTask;
        }
    }
}
