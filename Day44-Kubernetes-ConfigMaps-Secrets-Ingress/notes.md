# Deep Dive Notes: ConfigMaps, Secrets & Ingress Controllers

## 1. Atomic Symlink Volume Swapping
When a ConfigMap is mounted as a volume in a Pod:
```
/etc/config/appsettings.json -> /etc/config/..data/appsettings.json
/etc/config/..data -> /etc/config/..2026_10_23_14_00_00.123456789 (symlink)
```
When `kubectl apply` updates the ConfigMap, the kubelet creates a new timestamped directory and updates the `..data` symlink atomically using the `rename()` system call. This prevents race conditions where an application reads half-written config files.

## 2. Ingress Controller Architecture
Ingress resources are merely metadata declarations. The **Ingress Controller** is an application (running an NGINX, Envoy, or Traefik pod) watching the `kube-apiserver` for Ingress events. Upon changes, it dynamically regenerates the reverse proxy configuration (`nginx.conf`) and issues a reload without dropping active TCP connections.
