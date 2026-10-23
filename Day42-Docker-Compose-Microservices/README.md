# Day 42: Docker Compose for Microservice Stacks

## Overview
Day 42 focuses on production-grade multi-container orchestration with **Docker Compose**, managing interdependent microservice topologies including ASP.NET Core APIs, PostgreSQL 16, Redis 7, RabbitMQ 3.13, and Jaeger distributed tracing.

## Key Architectures
- **Bridge Network Isolation**: Dedicated `microservices-net` bridge preventing external network contamination and providing automatic DNS name resolution (`postgres`, `redis`, `rabbitmq`, `jaeger`).
- **Healthcheck Dependency Coordination**: `depends_on` with `condition: service_healthy` ensuring the ASP.NET Core API doesn't start until backing data stores and brokers are ready.
- **Volume Persistence**: Named volumes (`pgdata`, `redisdata`, `rabbitdata`) surviving container recreation.
- **Environment Variable Interpolation**: `${VAR:-default}` pattern decoupling environment-specific configuration from code.

## DSA Problem: LeetCode #198 & #213 (House Robber I & II)
- **LC #198 (House Robber)**: 1D Dynamic Programming with state $dp[i] = \max(dp[i-1], dp[i-2] + nums[i])$ in $\mathcal{O}(1)$ space.
- **LC #213 (House Robber II)**: Circular street constraint where house $0$ and house $N-1$ are adjacent. Decomposed into $\max(\text{Rob}(0, N-2), \text{Rob}(1, N-1))$.

## Test Verification
Run the 10 automated unit tests:
```bash
dotnet test tests/Day42.DockerCompose.Tests/Day42.DockerCompose.Tests.csproj
```
