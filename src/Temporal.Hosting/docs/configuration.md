# Configuration reference

The starter binds options from the `Temporal` section of `appsettings.json`,
overridden by `Temporal__*` environment variables. Every block is optional; a
minimal config is just the target host.

```json
{
  "Temporal": {
    "TargetHost": "localhost:7233",
    "Namespace": "default",
    "ApiKey": null,
    "Tls": null,
    "RpcRetry": {
      "InitialInterval": "00:00:00.100",
      "RandomizationFactor": 0.2,
      "Multiplier": 1.5,
      "MaxInterval": "00:00:05",
      "MaxElapsedTime": "00:00:10",
      "MaxRetries": 10
    },
    "KeepAlive": {
      "Interval": "00:00:30",
      "Timeout": "00:00:15"
    },
    "HttpConnectProxy": null,
    "DnsLoadBalancing": null,
    "GrpcCompression": {
      "Mode": "gzip"
    },
    "Namespaces": [ "payments", "orders" ],
    "Metrics": {
      "Enabled": true,
      "MeterName": "Temporal.Hosting",
      "UseDefaultInterceptor": true,
      "BaggageTagKeys": [],
      "PrometheusBindAddress": null,
      "OpenTelemetryUrl": null
    },
    "Tracing": {
      "Enabled": true,
      "UseDefaultInterceptor": true,
      "BaggageTagKeys": []
    },
    "Logging": {
      "Enabled": true,
      "Category": "Temporalio.Core"
    },
    "TestServer": {
      "Enabled": true,
      "Port": 0
    },
    "ConnectionWait": {
      "Enabled": true,
      "Timeout": "00:01:00",
      "InitialDelay": "00:00:01",
      "MaxDelay": "00:00:15"
    },
    "Workers": {
      "my-task-queue": {
        "MaxConcurrentActivities": 20,
        "MaxConcurrentWorkflowTasks": 100,
        "GracefulShutdownTimeout": "00:00:30",
        "MaxCachedWorkflows": 1000,
        "Deployment": {
          "DeploymentName": "my-app",
          "BuildId": "1.0",
          "UseWorkerVersioning": true,
          "DefaultVersioningBehavior": "Pinned"
        }
      }
    },
    "DataConverter": {
      "Encryption": {
        "Enabled": true,
        "Source": "config",
        "Key": "test-key-test-key-test-key-test!",
        "KeyId": "demo"
      },
      "ClaimCheck": {
        "Enabled": true,
        "Store": "filesystem",
        "ThresholdBytes": 1048576,
        "Directory": "claim-check"
      },
      "Secret": {
        "Enabled": true,
        "Source": "azureKeyVault",
        "SecretId": "ssn-key",
        "KeyId": "ssn-v1",
        "Encoding": "raw"
      }
    },
    "ActivityOptions": {
      "Default": {
        "ScheduleToCloseTimeout": "00:05:00",
        "HeartbeatTimeout": "00:00:30"
      },
      "LocalDefault": {
        "ScheduleToCloseTimeout": "00:00:10"
      },
      "Presets": {
        "long-running": {
          "ScheduleToCloseTimeout": "00:30:00",
          "HeartbeatTimeout": "00:01:00"
        },
        "fast": {
          "StartToCloseTimeout": "00:00:05"
        }
      }
    },
    "Workflows": {
      "Id": { "Format": "{Type:s}-{Guid:N}", "ChildFormat": "{Type:s}-{Guid:N}-{Parent}" },
      "Default": {
        "TaskQueue": "orders-queue",
        "RunTimeout": "00:05:00",
        "TaskTimeout": "00:00:10",
        "IdConflictPolicy": "UseExisting"
      },
      "ByType": {
        "MoneyTransferWorkflow": {
          "TaskQueue": "payments-queue",
          "RunTimeout": "00:30:00"
        },
        "ChildWorkflow": {
          "ParentClosePolicy": "RequestCancel",
          "CancellationType": "TryCancel"
        }
      }
    },
    "WorkflowSettings": {
      "Default": { "batchSize": 10 },
      "ByType": {
        "BatchingWorkflow": { "batchSize": 100 }
      }
    },
    "Schedules": {
      "nightly-cleanup": {
        "Action": {
          "Workflow": "CleanupWorkflow",
          "TaskQueue": "cleanup",
          "WorkflowId": "{Type:s}-cleanup",
          "RunTimeout": "00:05:00"
        },
        "Spec": {
          "Cron": [ "0 0 * * *" ],
          "TimeZoneName": "UTC"
        },
        "Policy": {
          "Overlap": "BufferAll",
          "CatchupWindow": "01:00:00",
          "PauseOnFailure": true
        },
        "State": { "Paused": false },
        "TriggerImmediately": false,
        "Reconcile": false
      }
    },
    "SearchAttributes": {
      "Enabled": true,
      "FailOnConflict": false,
      "Attributes": {
        "CustomerId": { "Type": "Keyword" },
        "Amount":     { "Type": "Double" }
      }
    },
    "HealthChecks": {
      "Enabled": true
    }
  }
}
```

Environment variables override the file (`Temporal__TargetHost`,
`Temporal__Metrics__Enabled`, ...). Each block is documented in its own page:

- [Worker registration](worker-registration.md) — `Workers`, `Namespaces`
- [Activity options](activity-options.md) — `ActivityOptions`
- [Workflow ops](workflow-ops.md) — `Workflows`, `WorkflowSettings`
- [Schedules](schedules.md) — `Schedules`
- [Search attributes](search-attributes.md) — `SearchAttributes`
- [Connection & TLS](connection.md) — `RpcRetry`, `KeepAlive`, `HttpConnectProxy`,
  `DnsLoadBalancing`, `GrpcCompression`, `ConnectionWait`, `Tls`
- [Observability](observability.md) — `Metrics`, `Tracing`, `Logging`
- [Health checks](health-checks.md) — `HealthChecks`
- [Payload codecs & secrets](data-converter.md) — `DataConverter` (Encryption,
  ClaimCheck, Secret)
- [Test server](connection.md#in-process-test-server) — `TestServer`
