using System;
using System.Diagnostics;
using OpenTelemetry.Collector;
using OpenTelemetry.Trace;

namespace Sample.MainApi.OpenTelemetry
{
    public class RabbitMQListener : ListenerHandler
    {
        public RabbitMQListener(string sourceName, Tracer tracer) : base(sourceName, tracer)
        {
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            this.Tracer.StartSpanFromActivity(activity.OperationName, activity);
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            var span = this.Tracer.CurrentSpan;
            span.End();
            if (span is IDisposable disposableSpan)
            {
                disposableSpan.Dispose();
            }
        }
    }

    public class RabbitMQCollector : IDisposable
    {
        private readonly Tracer tracer;
        private readonly DiagnosticSourceSubscriber subscriber;


        private static bool DefaultFilter(string activityName, object arg1, object unused)
        {
            return true;
        }

        public void Dispose()
        {
            this.subscriber?.Dispose();
        }

        public RabbitMQCollector(Tracer tracer)
        {
            this.tracer = tracer;
            this.subscriber = new DiagnosticSourceSubscriber(new RabbitMQListener("Sample.RabbitMQ", tracer), DefaultFilter);
            this.subscriber.Subscribe();
        }
    }
}
