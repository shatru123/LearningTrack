# Deep Dive Notes: Kubernetes Control Plane & Networking

## 1. Declarative Reconciliation Loop
Kubernetes operates on level-triggered reconciliation:
```
Current State (Observed by Kubelet) ----+
                                        |---> Controller reconciles delta ---> Desired State
Desired State (Stored in Etcd via API) -+
```
The Deployment controller monitors ReplicaSets; if desired is 3 and observed is 2, it issues Pod creation requests through the `kube-apiserver`.

## 2. ClusterIP vs NodePort vs LoadBalancer
- **ClusterIP**: Default service type assigning a virtual cluster-internal IP (VIP). Only reachable by workloads within the Kubernetes cluster.
- **NodePort**: Allocates a static port (default 30000–32767) across all cluster worker nodes. Traffic to `<NodeIP>:<NodePort>` is forwarded to backend pods.
- **LoadBalancer**: Provisions a cloud-provider external load balancer (AWS NLB/ALB, Azure Load Balancer, GCP Cloud LB) routing external internet traffic directly to the service endpoints.
