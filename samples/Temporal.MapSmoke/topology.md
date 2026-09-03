```mermaid
%%{init: {"theme":"base","themeVariables":{"background":"#ffffff","fontFamily":"Segoe UI, Helvetica, Arial, sans-serif"},"flowchart":{"useMaxWidth":true,"nodeSpacing":35,"rankSpacing":45}}}%%
flowchart LR
    classDef workflow fill:#e3f2fd,stroke:#1565c0,color:#000;
    classDef activity fill:#fff3e0,stroke:#ef6c00,color:#000;
    classDef nexus fill:#f3e5f5,stroke:#7b1fa2,color:#000;
    classDef unknown fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000;
    classDef contract fill:#ede7f6,stroke:#5e35b1,stroke-dasharray: 3 3,color:#000;
    classDef caller fill:#e0f7fa,stroke:#00838f,color:#000;

    n15["🖥 Program"]:::caller
    n16["⧉ IAmbiguousActivities.Run"]:::contract
    n23["❓ NexusOperation: Ship"]:::unknown
    n24["❓ NexusService: shipping"]:::unknown

    subgraph q0["📥 config-q-prod"]
    n27["ConfigFileWorkflow<br/><i>run: RunAsync(string → Task)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:235</sub>"]:::workflow
    end
    style q0 fill:#e8f5e9,stroke:#2e7d32,color:#000

    subgraph q1["📥 orders-fallback"]
    n33["OtherWorkflow<br/><i>run: RunAsync(string → Task)</i><br/><i>signal: Poke(→ Task)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:190</sub>"]:::workflow
    end
    style q1 fill:#e8f5e9,stroke:#2e7d32,color:#000

    subgraph q2["📥 queue-a"]
    n0["IGhostActivities.Vanish ❔<br/><sub>AppA/AppA.Contracts/IGhostActivities.cs:9</sub>"]:::activity
    n1["AmbiguousImplA.Run<br/><sub>AppA/AppA.Worker/Activities.cs:73</sub>"]:::activity
    n3["HeartbeatActivities.HeartbeatGood 💓<br/><sub>AppA/AppA.Worker/Activities.cs:56</sub>"]:::activity
    n4["HeartbeatActivities.HeartbeatMissing<br/><sub>AppA/AppA.Worker/Activities.cs:63</sub>"]:::activity
    n5["MainActivities.Counter<br/><sub>AppA/AppA.Worker/Activities.cs:14</sub>"]:::activity
    n6["MainActivities.Greet<br/><sub>AppA/AppA.Worker/Activities.cs:11</sub>"]:::activity
    n7["MainActivities.Local<br/><sub>AppA/AppA.Worker/Activities.cs:17</sub>"]:::activity
    n8["MainActivities.Uncalled<br/><sub>AppA/AppA.Worker/Activities.cs:20</sub>"]:::activity
    n9["OrderActivities.Process<br/><sub>AppA/AppA.Worker/Activities.cs:40</sub>"]:::activity
    n30["HeartbeatWorkflow<br/><i>run: RunAsync(string → Task)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:209</sub>"]:::workflow
    n31["MainWorkflow<br/><i>query: GetGreeting(→ string)</i><br/><i>run: RunAsync(string → Task)</i><br/><i>signal: AddGreetingAsync(string → Task)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:11</sub>"]:::workflow
    n32["OrderWorkflow<br/><i>query: GetStatus(→ string)</i><br/><i>run: RunAsync(string → Task&lt;string&gt;)</i><br/><i>signal: ApproveAsync(string → Task)</i><br/><i>update: SetPriorityAsync(int → Task&lt;string&gt;)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:120</sub>"]:::workflow
    n10_q2["OrderActivities.Ship ⚡<br/><sub>AppA/AppA.Worker/Activities.cs:47</sub>"]:::activity
    n29_q2["DualQueueWorkflow<br/><i>run: RunAsync(string → Task)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:101</sub>"]:::workflow
    end
    style q2 fill:#e8f5e9,stroke:#2e7d32,color:#000

    subgraph q3["📥 queue-b"]
    n12["BActivities.Process<br/><sub>AppA/../AppB/AppB.Worker/BActivities.cs:10</sub>"]:::activity
    n13["BActivities.ProcessOnlyB<br/><sub>AppA/../AppB/AppB.Worker/BActivities.cs:13</sub>"]:::activity
    n14["BActivities.RecordLegacyPayment<br/><sub>AppA/../AppB/AppB.Worker/BActivities.cs:18</sub>"]:::activity
    end
    style q3 fill:#e8f5e9,stroke:#2e7d32,color:#000

    subgraph q4["📥 queue-c"]
    n29_q4["DualQueueWorkflow<br/><i>run: RunAsync(string → Task)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:101</sub>"]:::workflow
    end
    style q4 fill:#e8f5e9,stroke:#2e7d32,color:#000

    subgraph q5["📥 standalone-q"]
    n10_q5["OrderActivities.Ship ⚡<br/><sub>AppA/AppA.Worker/Activities.cs:47</sub>"]:::activity
    end
    style q5 fill:#e8f5e9,stroke:#2e7d32,color:#000

    subgraph uq["❓ Unknown task queue"]
    n26["ChildWorkflow<br/><i>query: GetStatus(→ string)</i><br/><i>run: RunAsync(string → Task&lt;string&gt;)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:84</sub>"]:::workflow
    n28["ConfigQueueWorkflow<br/><i>run: RunAsync(string → Task)</i><br/><sub>AppA/AppA.Worker/Workflows.cs:110</sub>"]:::workflow
    end
    style uq fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000

    subgraph orp["🪤 Orphaned activities (no static caller)"]
    n2["AmbiguousImplB.Run<br/><sub>AppA/AppA.Worker/Activities.cs:79</sub>"]:::activity
    n11["OrphanActivities.NeverReferenced<br/><sub>AppA/AppA.Worker/Activities.cs:28</sub>"]:::activity
    end
    style orp fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000

    n15 -->|"standalone [ScheduleToClose=10s]"| n10_q2
    n15 --> n31
    n15 --> n32
    n15 --> n32
    n15 --> n32
    n26 -->|"#1 [StartToClose=10s]"| n6
    n30 <-->|"#1 [StartToClose=10s; HeartbeatTimeout=10s]"| n3
    n30 --x|"#2 [StartToClose=10s; HeartbeatTimeout=5s]"| n4
    n31 -->|"#4 [StartToClose=10s]"| n0
    n31 -->|"#2 🔁 [StartToClose=10s]"| n5
    n31 -->|"#1, #5 [StartToClose=10s]"| n6
    n31 --o|"#6 [StartToClose=10s]"| n7
    n31 -->|"#7 [StartToClose=10s]"| n12
    n31 -->|"#3 [StartToClose=10s]"| n14
    n31 ==> n23
    n31 ==> n24
    n31 -.-> n26
    n32 -->|"#1 [StartToClose=30s; Retry:max3]"| n9
    n32 -->|"#2, #3 🔁 [StartToClose=10s]"| n10_q2
    n32 -->|"#4 [StartToClose=10s]"| n16
    n32 -.-> n32
    n32 --> n33
    linkStyle 7 stroke:#c62828,stroke-dasharray: 4 3
    n15 ~~~ uq
    uq ~~~ orp
```

## Legend

| Marker | Meaning |
| --- | --- |
| Blue box | Workflow |
| Orange ellipse | Activity |
| Purple diamond | Nexus operation |
| Green `📥` box | Task queue — contained nodes run on it |
| `🖥` teal box | Caller (client-side entry point) |
| `⧉` dashed box | Contract (ambiguous interface impls) |
| Red dashed box | Unknown task queue / orphaned activities |
| `⚡` | Standalone activity |
| `💓` | Activity calls `Heartbeat(...)` |
| `-->` | Activity call — `#1, #3` call order, `🔁` inside a loop |
| `--o` | Local activity call |
| `-.->` | Child workflow |
| `==>` | Nexus operation/service |
| `<-->` | Activity heartbeats back to its caller |
| `--x` (red) | Issue: heartbeat timeout set, but no heartbeat call |
| `❓` | String-named target not resolved by name |
| `❔` | Contract member with no implementation |
| `<sub>Repo/Path:Line</sub>` | Where the node lives; `?` when unknown |
| Multi-queue nodes | Duplicated inside every queue they belong to |