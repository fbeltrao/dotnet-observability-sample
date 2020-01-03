using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Sample.MainApi.ApplicationInsights
{
    internal class DiagnosticSourceListener : IObserver<KeyValuePair<string, object>>
    {
        public DiagnosticSourceListener()
        {
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (Activity.Current == null)
            {
                //CollectorEventSource.Log.NullActivity(value.Key);
                return;
            }

            try
            {
                if (value.Key.EndsWith("Start"))
                {
                    OnStartActivity(Activity.Current, value.Value);
                }
                else if (value.Key.EndsWith("Stop"))
                {
                    this.OnStopActivity(Activity.Current, value.Value);
                }
                else if (value.Key.EndsWith("Exception"))
                {
                    this.OnException(Activity.Current, value.Value);
                }
                else
                {
                    this.OnCustom(value.Key, Activity.Current, value.Value);
                }
            }
            catch (Exception)
            {
                //CollectorEventSource.Log.UnknownErrorProcessingEvent(this.handler?.SourceName, value.Key, ex);
            }
        }

        protected virtual void OnCustom(string key, Activity current, object value)
        {        
        }

        protected virtual void OnException(Activity current, object value)
        {
        }

        protected virtual void OnStopActivity(Activity current, object value)
        {
        }

        protected virtual void OnStartActivity(Activity current, object value)
        {
        }
    }

    internal class RabbitMQSourceListener : DiagnosticSourceListener
    {
        private readonly TelemetryClient client;

        public RabbitMQSourceListener(TelemetryClient client)
        {
            this.client = client;
        }

        protected override void OnStopActivity(Activity current, object value)
        {
            using var dependency = client.StartOperation<DependencyTelemetry>(current);
            dependency.Telemetry.Type = "rabbitmq";
        }
    }

    public class RabbitMQCollector : IObserver<DiagnosticListener>
    {
        private readonly TelemetryClient client;
        private long disposed;
        private List<IDisposable> listenerSubscriptions;
        private IDisposable allSourcesSubscription;


        public RabbitMQCollector(TelemetryClient client)
        {
            this.client = client;
            this.listenerSubscriptions = new List<IDisposable>();
        }

        public void Subscribe()
        {
            if (this.allSourcesSubscription == null)
            {
                this.allSourcesSubscription = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            //

        }

        public void OnNext(DiagnosticListener value)
        {
            if ((Interlocked.Read(ref this.disposed) == 0))
            {
                if (value.Name == "Sample.RabbitMQ")
                {
                    var listener = new RabbitMQSourceListener(client);
                    var subscription = value.Subscribe(listener);

                    lock (this.listenerSubscriptions)
                    {
                        this.listenerSubscriptions.Add(subscription);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) == 1)
            {
                // already disposed
                return;
            }

            lock (this.listenerSubscriptions)
            {
                foreach (var listenerSubscription in this.listenerSubscriptions)
                {
                    listenerSubscription?.Dispose();
                }

                this.listenerSubscriptions.Clear();
            }

            this.allSourcesSubscription?.Dispose();
            this.allSourcesSubscription = null;
        }
    }


    public class RabbitMQApplicationInsightsModule : ITelemetryModule, IDisposable
    {
        private RabbitMQCollector collector;
        public RabbitMQApplicationInsightsModule()
        {

        }

        public void Initialize(TelemetryConfiguration configuration)
        {
            if (collector != null)
                return;

            collector = new RabbitMQCollector(new TelemetryClient(configuration));
            collector.Subscribe();
        }

        public void Dispose()
        {
            collector?.Dispose();
        }
    }
}
