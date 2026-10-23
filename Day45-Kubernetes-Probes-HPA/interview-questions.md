# Day 45: Senior / Staff Engineering Interview Q&As

### Q1: Why should you NEVER check downstream database or external API connectivity inside a Liveness Probe?
**Answer**:
A Liveness Probe answers the question: *"Is the process deadlocked or fatally broken such that restarting it will fix it?"*
If downstream database or network connectivity fails, restarting the application process will **not** fix the database. If 50 pods all fail their liveness probe simultaneously due to a database blip, Kubernetes will kill and restart all 50 pods at once, causing a thundering herd on startup, crashing the database further, and amplifying a minor glitch into a total outage.
External dependencies must **only** be checked in the **Readiness Probe**.

### Q2: How does the Startup Probe solve the dilemma of slow application boot times?
**Answer**:
Before Startup Probes, developers increased `initialDelaySeconds` on Liveness Probes (e.g. to 120s). However, if the application crashed during normal operation, Kubernetes would take a full 120s plus threshold intervals to detect the crash and restart the pod.
A Startup Probe allows setting a high threshold (e.g. `failureThreshold: 30`, `periodSeconds: 10` = 300s) during boot. The moment it passes once, the Startup Probe disables itself permanently and the fast Liveness Probe (`periodSeconds: 10`, `timeoutSeconds: 2`) takes over immediately.

### Q3: What is "flapping" in auto-scaling, and how does Kubernetes HPA mitigate it?
**Answer**:
Flapping (or thrashing) occurs when a workload scales up due to high load, the increased replica count immediately drops average metric utilization, triggering a rapid scale-down, which in turn causes utilization to spike again in a continuous vicious cycle.
HPA mitigates flapping via:
1. **10% Tolerance Band**: Ignores metric fluctuations within $\pm 10\%$ of the target.
2. **Scale-Down Stabilization Window**: Records all computed replica recommendations over the last 300 seconds (5 minutes) and selects the maximum recommended replica count.

### Q4: Explain the DP algorithm for LeetCode #322 Coin Change.
**Answer**:
Let $dp[a]$ be the minimum coins required to make amount $a$.
- Initialize $dp[0] = 0$ and $dp[1 \dots \text{amount}] = \infty$.
- For each amount $a$ from 1 to `amount`:
  For each coin $c$ in `coins`:
  If $a - c \ge 0$:
  $$dp[a] = \min(dp[a], 1 + dp[a - c])$$
- If $dp[\text{amount}] == \infty$, return -1; else return $dp[\text{amount}]$.
Time complexity is $\mathcal{O}(\text{Amount} \times \text{Coins.Length})$ and space complexity is $\mathcal{O}(\text{Amount})$.

### Q5: Why must you track both `minProduct` and `maxProduct` in LeetCode #152 Maximum Product Subarray?
**Answer**:
Unlike maximum sum (Kadane's algorithm), multiplying by a negative number reverses the order of magnitude: a large negative number multiplied by another negative number becomes a huge positive number.
Therefore, at each element $x$:
- If $x < 0$, swap `maxProd` and `minProd`.
- `maxProd = max(x, maxProd * x)`
- `minProd = min(x, minProd * x)`
- `globalMax = max(globalMax, maxProd)`
This maintains optimal state in $\mathcal{O}(N)$ time and $\mathcal{O}(1)$ space.

### Q6: How do you configure ASP.NET Core Health Checks for separate `/health/live` and `/health/ready` endpoints?
**Answer**:
Tag health checks during DI registration:
```csharp
services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddNpgSql(connectionString, tags: new[] { "ready" })
    .AddRedis(redisConnectionString, tags: new[] { "ready" });

app.MapHealthChecks("/health/live", new HealthCheckOptions {
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions {
    Predicate = check => check.Tags.Contains("ready")
});
```

### Q7: How does HPA scale workloads based on custom Prometheus metrics like HTTP Requests Per Second (RPS)?
**Answer**:
1. Deploy **Prometheus Adapter** (`k8s-prometheus-adapter`) which translates Prometheus PromQL queries into Kubernetes Custom Metrics API (`custom.metrics.k8s.io`).
2. Configure the HPA metric rule:
```yaml
metrics:
  - type: Pods
    pods:
      metric:
        name: http_requests_per_second
      target:
        type: AverageValue
        averageValue: 1000
```
3. HPA queries the Custom Metrics API, aggregates RPS across pods, and scales replicas up when average RPS exceeds 1000.

### Q8: What is the impact of missing or misconfigured Resource Requests when configuring HPA with CPU utilization?
**Answer**:
HPA computes CPU utilization as a percentage of the Pod's **CPU Request**, NOT CPU Limit or node capacity.
If a Pod has no CPU Request defined:
$$\text{Utilization} = \frac{\text{Current CPU}}{\text{Request (null)}} = \text{undefined}$$
The HPA controller will fail with `unable to get metrics for resource cpu: missing request for cpu` and will refuse to scale the deployment.

### Q9: What happens if a Pod's readinessProbe fails while it is serving active HTTP requests?
**Answer**:
1. The kubelet marks the Pod `Ready: False`.
2. The Endpoints controller removes the Pod's IP from the Service's Endpoints object.
3. Kube-proxy removes the forwarding rules; ingress and other pods stop routing **new** traffic to this Pod.
4. Existing in-flight TCP connections remain open and continue processing until finished or closed by the client.

### Q10: How do you prevent auto-scaler scale-up delays for sudden traffic bursts?
**Answer**:
1. Configure `behavior.scaleUp.stabilizationWindowSeconds: 0` for immediate scale-up reaction.
2. Use `selectPolicy: Max` with both percentage (e.g. `value: 100%`) and absolute pod limits (e.g. `value: 4 pods`).
3. Set aggressive CPU target utilization (e.g. 60-70%) to give existing replicas buffer room while new pods are scheduled and pulled.
4. Use **KEDA** (Kubernetes Event-driven Autoscaling) to scale proactively based on external queue lag (e.g. RabbitMQ messages ready) rather than reactive CPU consumption.
