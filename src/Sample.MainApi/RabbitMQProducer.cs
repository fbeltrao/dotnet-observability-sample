using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Sample.Common;

namespace Sample.MainApi
{
    public class RabbitMQProducer : IRabbitMQProducer, IDisposable
    {
        static DiagnosticSource diagnosticSource = new DiagnosticListener("Sample.RabbitMQ");

        public string HostName { get; private set; }
        public string QueueName { get; private set; }

        private IConnection connection;
        private IModel channel;

        public RabbitMQProducer(IConfiguration configuration)
        {
            HostName = configuration.GetRabbitMQHostName();
            QueueName = Constants.WebQueueName;

            this.connection = new ConnectionFactory
            {
                HostName = HostName
            }.CreateConnection();

            this.channel = this.connection.CreateModel();
            channel.QueueDeclare(queue: Constants.FirstQueueName, exclusive: false);
        }

        public void Publish(string message)
        {
            Activity activity = null;
            if (diagnosticSource.IsEnabled("Sample.RabbitMQ"))
            {
                activity = new Activity("Publish to RabbitMQ");
                activity.AddTag("operation", "publish");
                activity.AddTag("host", HostName);
                activity.AddTag("queue", QueueName);
                diagnosticSource.StartActivity(activity, null);
            }
            
            var props = channel.CreateBasicProperties();
            props.Headers = new Dictionary<string, object>();
            props.Headers.Add(TraceParent.HeaderKey, TraceParent.FromCurrentActivity().ToString());

            channel.BasicPublish("", QueueName, props, System.Text.Encoding.UTF8.GetBytes(message));

            if (activity != null)
            {
                diagnosticSource.StopActivity(activity, null);
            }
        }

        public void Dispose()
        {
            this.channel?.Close();
            this.connection?.Close();
        }
    }
}
