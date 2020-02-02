# Implementing observability in .NET Core stack

This document takes a look at current options to implement observability in .NET Core stack. For a quick peek into the sample application check section [Quickly run sample application](#Quickly-run-sample-application)

**DISCLAIMER 1**: I am familiar with Azure Application Insights. I don't have the same experience with the OSS options out there, therefore the sample code covers only Jaeger and Prometheus (at least for now).

**DISCLAIMER 2**: At the time of writing OpenTelemetry .NET SDK is in alpha stage, available through nightly builds. The sample project might stop working as the SDK matures and breaking changes are introduced.

## Observability

Agile, devops, continuous delivery are terms used nowadays in modern software development practices. The idea is to change/fail fast and update constantly. In order to be able to deliver fast it is fundamental to have tools monitoring how the running system behaves. This tool(s) contains required information needed to separate a good update from a bad. Remediating a bad update usually means rolling back to previous version and working on the identified issues. Combined with progressive deployment strategies (canary, mirroring, rings, blue/green, etc.) the impact of a bad update can be minimized.

The following information can be used to identify bad updates:

- Are we observing more errors than before? Are we getting new error types?
- Did the request duration unexpectedly increase compared to previous version?
- Did the throughput (req/sec) unexpectedly decrease?
- Did the CPU and/or Memory usage unexpectedly increase?
- Did we notice changes in our KPIs? Are we selling less items? Did our visitor count decrease?

This observability is typically built by 3 pillars:

- Logging: collects information about events happening in the system, helping the team identifying unexpected application behavior
- Tracing: collects information creating an end-to-end view of how transactions are executed in a distributed system
- Metrics: provide a near real-time indication of how the system is running. As opposed to logs and traces, the amount of data collected using metrics remains constant as the system load increases

## Adding observability to .NET Core Stack

The sample application contained in this repository only takes a look at observability in a .NET Core application. It **does not** cover infrastructure observability.

### Logging

.NET Core provides a standard API supporting logging, as described [here](https://docs.microsoft.com/aspnet/core/fundamentals/logging/). Logging in .NET Core is [distributed tracing aware](https://devblogs.microsoft.com/aspnet/improvements-in-net-core-3-0-for-troubleshooting-and-monitoring-distributed-apps/) out of the box. There is support for 3rd party providers, allowing you to choose the logging backend of your preference.

When deciding a logging platform, consider the following features:

- Centralized: allowing the collection/storage of all system logs in a central location
- Structured logging: allows you to add searchable metadata to logs
- Searchable: allows searching by multiple criteria (app version, date, category, level, text, metadata, etc.)
- Configurable: allows changing verbosity without code changes (based on log level and/or scope)
- [Nice to have] Integrated: integrated into distributed tracing

In the provided sample application [Azure Application Insights logging extension](https://docs.microsoft.com/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1#azure-application-insights-trace-logging) is used. The extension exports logs into Application Insights.

### Tracing and Metrics

In September 2019 [OpenTelemetry](https://opentelemetry.io/) CNCF sandbox project started, aiming to standardize metrics and tracing collection. The idea is to add observability to your code regardless of the tools used to store, view and analyse the collected information.

Before OpenTelemetry (or it's predecessors OpenCensus and OpenTracing), adding observability would often mean adding proprietary SDKs (in)directly to the code base.

The current state of the OpenTelemetry .NET SDK is still in alpha.  Azure Monitor Application Insights team investing in OpenTelemetry as a next step of Azure Monitor SDKs evolution. In the sample code provided I am using Application Insights SDK as well to compare results with a tool I am familiar with.

### Quick explanation about tracing

Tracing collects required information to enable the observation of a transaction as it is "walks" through the system. It must be implemented in every service taking part of the transaction to be effective.

Simplified, [OpenTelemetry collects traces using spans](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/overview.md) (operations in Application Insights). A span has a unique identifier (SpanId, 16 characters, 8 bytes) and a trace identifier (TraceId, 32 characters, 16 bytes). The trace identifier is used to correlate all operations for a given transaction. A span can contain 0..* children spans.

Application Insights have different names for spans and their identifiers. The table below has a summary of them:

|Application Insights|OpenTelemetry|
|-|-|
|Request, PageView|Span with span.kind = server|
|Dependency|Span with span.kind = client|
|Id of Request and Dependency|SpanId|
|Operation_Id|TraceId|
|Operation_ParentId|Reference of type ChildOf (the parent SpanId)|

### SDK and tools used

In order to collect information in the sample application the following libraries are used:

- [Azure Application Insight](https://github.com/microsoft/ApplicationInsights-dotnet)
- [OpenTelemetry](https://github.com/open-telemetry/opentelemetry-dotnet) nightly builds (in alpha at the time of writing)
- [Standard logging provided by .NET Core](https://docs.microsoft.com/aspnet/core/fundamentals/logging/)

Collected information can be exported to [Azure Application Insights](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview) (Logs, Tracing, Metrics), [Jaeger](https://www.jaegertracing.io/) (Tracing) or [Prometheus](https://prometheus.io/) (Metrics).

For information on how to **bootstrap a project with OpenTelemetry** check the [documentation](https://github.com/open-telemetry/opentelemetry-dotnet).

To **bootstrap your project with Application Insights** check the SDK documentation [here](https://docs.microsoft.com/azure/azure-monitor/app/asp-net-core) and [here](https://docs.microsoft.com/azure/azure-monitor/app/worker-service) for non-http applications. Keep in mind that OpenTelemetry also supports Azure Application Insights as one of the collector destinations.

## Sample scenarios

To illustrate how observability could be add to a .NET Core application this article goes through 3 scenarios where predefined requirements are implemented.
For project setup information please check the source code or SDK documentation.

- [Scenario 1: REST API accessing SQL Server](./scenario1.md)
- [Scenario 2: REST API call chain](./scenario2.md)
- [Scenario 3: Asynchronous transactions](./scenario3.md)

Before continue reading, **please go through the 3 sample scenarios**.

## Quickly run sample application

To quickly see the application running using pre-built docker images and docker-compose follow the guideline below:

### Using OpenTelemetry

1. Clone this repository
1. Open terminal under `ready-to-run\sample`
1. Execute `docker-compose up`
1. Generate load in terminal with

```bash
watch -n 2 curl --request GET http://localhost:5001/api/enqueue/WebSiteA
```

for PowerShell use this script:

```Powershell
while (1) {Invoke-WebRequest -Uri http://localhost:5001/api/enqueue/WebSiteA; sleep 2}
```

5. View traces in [Jaeger](http://localhost:16686/)
6. View metrics by searching for "Enqueued_Item" in [Prometheus](http://localhost:9090)
7. Build dashboards in [Grafana](http://localhost:3000/) (admin/password1)

### Using Application Insights SDK

1. Clone this repository
1. Open terminal under `ready-to-run\sample`
1. Create file `ready-to-run\sample\.env` with following content:

```env
USE_APPLICATIONINSIGHTS=true
USE_OPENTELEMETRY=false
AI_INSTRUMENTATIONKEY=<ENTER-APPLICATION-INSIGHTS-INSTRUMENTATION-KEY>
```

4. Execute `docker-compose up`
5. Generate load in terminal with

```bash
watch -n 2 curl --request GET http://localhost:5001/api/enqueue/WebSiteA
```

for PowerShell use this script:

```Powershell
while (1) {Invoke-WebRequest -Uri http://localhost:5001/api/enqueue/WebSiteA; sleep 2}
```

6. View logs, traces and metrics in Azure Portal Application Insights

## Conclusion

OpenTelemetry is positioning itself as a strong candidate as a standard API for tracing and metrics collection. That becomes even more important when building polyglot systems as OpenTelemetry SDK supports multiple languages using the same idiom (even though other vendors usually support multiple languages).

The short term problem are related to the early stage of the SDK, reflecting in missing features and production ready versions. Here a matrix comparing OpenTelemetry and Azure Application Insights:

|SDK|State|Http|Sql|Azure Services|Exporters|
|-|-|-|-|-|-|
|[Application Insights](https://github.com/microsoft/ApplicationInsights-dotnet)|GA|Yes|Yes|Yes|Application Insights|
|[Open Telemetry](https://github.com/open-telemetry/opentelemetry-dotnet)|Alpha (December 2019)|Yes|No|Yes|Application Insights<br/>Jaeger</br>Zipkin<br/>Stackdriver<br/>Prometheus<br/>[and more](https://github.com/open-telemetry/opentelemetry-dotnet#exporters-packages)

For applications going to production soon sticking with proprietary SDKs is probably the safest choice, as the maturity and features are superior. An abstraction on top of the SDK makes the implementation interchangeable. However, this is exactly the value of OpenTelemetry.

When choosing a observability platform I, whenever possible, prefer to stick with a centralized solution containing all collected information. Azure Monitor / Application Insights and Stackdriver are some of the examples. The sample project demonstrates how Application Insights displays logs in the scope of a trace.

However, some projects have dependencies on specific vendors (i.e. Prometheus metrics for scaling or progressive deployment), which limits the choices. OpenTelemetry, as a higher level abstraction of instrumented code, has a promise to allow combining various exporters in a single app. So progressive deployment can be controlled with the subset of metrics collected by Prometheus while bigger set of metrics, plus logs and traces are exported to the centralized observability platform.

Another deciding factor is minimizing vendor locking, allowing the system to be agnostic of hosting environment. In that case, sticking with an OSS solution is favoured.

## Appendix

- [State of Application Insights OpenTelemetry exporter](./opentelemetry-ai-state.md)
- [Application Insights tips](./ai-tips.md)
