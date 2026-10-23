# Day 42: Senior / Staff Engineering Interview Q&As

### Q1: What is the critical difference between `depends_on` with and without `condition: service_healthy` in Docker Compose?
**Answer**:
A bare `depends_on: [postgres]` only waits for the PostgreSQL container process to be spawned (`ENTRYPOINT` started). It does not wait for the database engine to finish initializing, replay logs, or open socket connections. This causes downstream services to crash immediately with connection refused errors.
With `condition: service_healthy`, Compose queries the Docker daemon's health monitoring daemon and blocks downstream container creation until the healthcheck command (such as `pg_isready -U user -d db`) returns exit code `0` consecutively `retries` times.

### Q2: How does Docker Compose implement internal DNS resolution between services?
**Answer**:
When containers join a user-defined network, Docker starts an embedded DNS server on `127.0.0.11`. Whenever an application attempts to resolve a hostname like `http://rabbitmq:5672`, the container's resolver routes the DNS query to `127.0.0.11`, which maps the service name or container aliases to the container's current internal IP address on the bridge network.

### Q3: How do you prevent port collision when running multiple compose stacks on a developer machine?
**Answer**:
Avoid hardcoding host ports in production compose files. Instead, use environment variable interpolation (e.g., `"${API_PORT:-8080}:8080"`), or leave the host port empty (`"8080"`) to let Docker assign ephemeral random host ports, inspected via `docker compose port order-api 8080`.

### Q4: How should secrets and connection strings be injected in production Docker Compose?
**Answer**:
Never commit unencrypted credentials to `docker-compose.yml`. Use Docker Compose Secrets (`secrets:` top-level block with `file:` or `environment:`) or load credentials from an `.env` file that is excluded via `.gitignore`. In cloud environments, reference runtime secret injection tools such as AWS Secrets Manager or HashiCorp Vault.

### Q5: What is the difference between Docker `bridge` and `host` network drivers?
**Answer**:
`bridge` provides isolated network namespace with its own IP and subnet, requiring explicit port mapping (`-p 8080:8080`). `host` removes network namespace isolation, binding the container's network directly to the host's network interfaces, eliminating NAT packet rewriting overhead at the expense of port conflict risks and loss of internal container name resolution.

### Q6: How does Docker Compose handle container restart policies on microservice crashes?
**Answer**:
Compose supports four restart policies:
1. `no`: Default, never restarts container.
2. `always`: Always restarts container on exit, even on system boot unless explicitly stopped.
3. `on-failure[:max-retries]`: Restarts only if the container exits with a non-zero exit code.
4. `unless-stopped`: Similar to `always`, but retains stopped state across daemon reboots if stopped manually.

### Q7: Explain the dynamic programming state transition for LeetCode #198 House Robber.
**Answer**:
Let $dp[i]$ represent the maximum money robbable from the first $i$ houses:
$$dp[i] = \max(dp[i-1], dp[i-2] + nums[i])$$
Since $dp[i]$ depends only on the previous two values, we can discard the array and maintain two scalar variables (`prev1`, `prev2`), optimizing space complexity from $\mathcal{O}(N)$ to $\mathcal{O}(1)$.

### Q8: How do you solve the circular dependency in LeetCode #213 House Robber II?
**Answer**:
Because the first house and last house cannot both be robbed, the circular street yields two mutually exclusive scenarios:
1. Rob houses from index $0$ to $N-2$ (ignoring the last house).
2. Rob houses from index $1$ to $N-1$ (ignoring the first house).
We run the linear House Robber algorithm on both slices and take $\max(\text{rob}(0, N-2), \text{rob}(1, N-1))$.

### Q9: How can you optimize Docker image build caching within a Docker Compose build context?
**Answer**:
Structure the Dockerfile so frequently modified files (source code) appear after infrequently modified files (package descriptors). In .NET:
1. `COPY *.csproj .` and run `dotnet restore`.
2. `COPY . .` and run `dotnet build / publish`.
In Compose, specify `cache_from:` pointing to pre-built registry images to leverage remote layer caches in CI/CD.

### Q10: How do named volumes differ from bind mounts in Docker Compose?
**Answer**:
Named volumes are isolated in Docker's storage driver directory (`/var/lib/docker/volumes`), have predictable read/write performance across Linux, Mac, and Windows, and support volume drivers (NFS, cloud storage). Bind mounts mount arbitrary host paths directly, requiring identical file permissions (UID/GID) between host and container.
