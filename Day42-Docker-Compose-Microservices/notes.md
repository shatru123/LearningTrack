# Deep Dive Notes: Docker Compose for Microservice Stacks

## 1. Network Bridging & Service Discovery
In Docker Compose, custom networks create isolated virtual ethernet bridges (veth pairs). Every container registered on the network is assigned an internal IP address and can resolve other containers via their service name or container name using Docker's internal embedded DNS server (`127.0.0.11`).

## 2. Healthcheck Coordination vs Simple `depends_on`
A plain `depends_on` directive only guarantees that container A starts after container B process execution begins. It does not wait for database initialization, schema migration, or port binding.
Using `condition: service_healthy` ensures that Docker's daemon executes the specified `healthcheck.test` command (e.g. `pg_isready`, `redis-cli ping`, `rabbitmq-diagnostics check_running`) until the consecutive success threshold is reached before unblocking dependent containers.

## 3. Volume Persistence Strategies
- **Named Volumes**: Managed by the Docker daemon in `/var/lib/docker/volumes/`, offering high performance, automatic directory creation, and OS-agnostic mount paths.
- **Bind Mounts**: Direct host filesystem bindings (`./data:/var/lib/data`), ideal for local source code syncing during development but dangerous in production due to permission mismatches.
