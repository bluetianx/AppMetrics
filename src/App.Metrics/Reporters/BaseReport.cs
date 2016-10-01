﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using App.Metrics.MetricData;
using App.Metrics.Utils;

namespace App.Metrics.Reporters
{
    public abstract class BaseReport : MetricsReport
    {
        private CancellationToken _token;

        protected DateTime CurrentContextTimestamp { get; private set; }

        protected DateTime ReportTimestamp { get; private set; }

        public void RunReport(MetricsData metricsData, Func<HealthStatus> healthStatus, CancellationToken token)
        {
            _token = token;

            ReportTimestamp = Clock.Default.UTCDateTime;

            StartReport(metricsData.Context);

            ReportContext(metricsData, Enumerable.Empty<string>());

            ReportHealthStatus(healthStatus);

            EndReport(metricsData.Context);
        }

        protected abstract void ReportCounter(string name, CounterValue value, Unit unit, MetricTags tags);

        protected abstract void ReportGauge(string name, double value, Unit unit, MetricTags tags);

        protected abstract void ReportHealth(HealthStatus status);

        protected abstract void ReportHistogram(string name, HistogramValue value, Unit unit, MetricTags tags);

        protected abstract void ReportMeter(string name, MeterValue value, Unit unit, TimeUnit rateUnit, MetricTags tags);

        protected abstract void ReportTimer(string name, TimerValue value, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, MetricTags tags);

        protected virtual void EndContext(string contextName)
        {
        }

        protected virtual void EndMetricGroup(string metricName)
        {
        }

        protected virtual void EndReport(string contextName)
        {
        }

        protected virtual string FormatContextName(IEnumerable<string> contextStack, string contextName)
        {
            var stack = string.Join(" - ", contextStack);
            if (stack.Length == 0)
            {
                return contextName;
            }
            return string.Concat(stack, " - ", contextName);
        }

        protected virtual string FormatMetricName<T>(string context, MetricValueSource<T> metric)
        {
            return string.Concat("[", context, "] ", metric.Name);
        }

        protected virtual void ReportEnvironment(string name, IEnumerable<EnvironmentEntry> environment)
        {
        }

        protected virtual void StartContext(string contextName)
        {
        }

        protected virtual void StartMetricGroup(string metricName)
        {
        }

        protected virtual void StartReport(string contextName)
        {
        }

        private void ReportContext(MetricsData data, IEnumerable<string> contextStack)
        {
            CurrentContextTimestamp = data.Timestamp;
            var contextName = FormatContextName(contextStack, data.Context);

            StartContext(contextName);

            ReportEnvironment(contextName, data.Environment);

            ReportSection("Gauges", data.Gauges, g => ReportGauge(FormatMetricName(contextName, g), g.Value, g.Unit, g.Tags));
            ReportSection("Counters", data.Counters, c => ReportCounter(FormatMetricName(contextName, c), c.Value, c.Unit, c.Tags));
            ReportSection("Meters", data.Meters, m => ReportMeter(FormatMetricName(contextName, m), m.Value, m.Unit, m.RateUnit, m.Tags));
            ReportSection("Histograms", data.Histograms, h => ReportHistogram(FormatMetricName(contextName, h), h.Value, h.Unit, h.Tags));
            ReportSection("Timers", data.Timers,
                t => ReportTimer(FormatMetricName(contextName, t), t.Value, t.Unit, t.RateUnit, t.DurationUnit, t.Tags));

            var stack = contextStack.Concat(new[] { data.Context });
            foreach (var child in data.ChildMetrics)
            {
                ReportContext(child, stack);
            }

            EndContext(contextName);
        }

        private void ReportHealthStatus(Func<HealthStatus> healthStatus)
        {
            var status = healthStatus();
            if (!status.HasRegisteredChecks)
            {
                return;
            }
            StartMetricGroup("Health Checks");
            ReportHealth(status);
        }

        private void ReportSection<T>(string name, IEnumerable<T> metrics, Action<T> reporter)
        {
            if (_token.IsCancellationRequested)
            {
                return;
            }

            if (metrics.Any())
            {
                StartMetricGroup(name);
                foreach (var metric in metrics)
                {
                    if (_token.IsCancellationRequested)
                    {
                        break;
                    }

                    reporter(metric);
                }
                EndMetricGroup(name);
            }
        }
    }
}