# Deploying Finnova to local Kubernetes

Runs the four backend services as separate Deployments:

| Service                       | Port | Exposed?            | DB connection key (app)                | Secret key (`finnova-db`)     |
|-------------------------------|------|---------------------|----------------------------------------|-------------------------------|
| `finnova-api-gateway`         | 5000 | Yes (NodePort 30500)| —                                      | —                             |
| `finnova-ua-service`          | 5010 | Internal only       | `ConnectionStrings:DefaultConnection`  | `uaConnectionString`          |
| `finnova-accounts-service`    | 5020 | Internal only       | `ConnectionStrings:FinnovaConnection`  | `accountsConnectionString`    |
| `finnova-systemadmin-service` | 5030 | Internal only       | `ConnectionStrings:FinnovaConnection`  | `systemadminConnectionString` |

Each data service pulls its OWN connection string from a dedicated key in the
`finnova-db` Secret, so the three services can point at different databases or use
different SQL logins independently. They are seeded pointing at the same `Finnova`
database; change any one of them without affecting the others.

The gateway reaches the backends by their Service DNS names, which match the cluster
addresses already set in `Finnova.ApiGateway/appsettings.json`. Only the gateway is
exposed to the host, mirroring the real architecture where it is the sole origin.

## Prerequisites

- Docker + a local Kubernetes cluster (Docker Desktop, minikube, or kind).
- SQL Server running on your **host** machine, because the services point at
  `host.docker.internal`. It must:
  - Accept **SQL authentication** (a username/password login). Windows Integrated /
    `Trusted_Connection` cannot work from a Linux container — that is why the manifest
    uses a SQL login, not the original `(localdb)` / `Trusted_Connection` string.
  - Listen on TCP 1433 and allow remote connections through the firewall.
  - Have each target database created and migrated (one `Finnova` DB by default, or a
    separate DB per service if you split the connection strings).

## 1. Edit the host DB credentials

Open `k8s/finnova.yaml` and set the real SQL login(s) in the `finnova-db` Secret. There
is now one key per data service — edit them independently:

```yaml
stringData:
  uaConnectionString:          "Server=host.docker.internal,1433;Database=Finnova;User Id=<login>;Password=<password>;TrustServerCertificate=True;Encrypt=False"
  accountsConnectionString:    "Server=host.docker.internal,1433;Database=Finnova;User Id=<login>;Password=<password>;TrustServerCertificate=True;Encrypt=False"
  systemadminConnectionString: "Server=host.docker.internal,1433;Database=Finnova;User Id=<login>;Password=<password>;TrustServerCertificate=True;Encrypt=False"
```

To give a service its own database, change only its `Database=` (for example
`Database=Finnova_SystemAdmin`) and make sure that database exists and is migrated.

> minikube note: `host.docker.internal` works on the Docker driver. On other drivers,
> use `minikube ssh 'grep host.minikube /etc/hosts'` or the host IP shown by
> `minikube ip`-gateway, and substitute it for `host.docker.internal`.

## 2. Build the images (from the solution root)

```powershell
cd d:\Projects\Finnova\Finnova-API

docker build -t finnova-ua-service:local          -f Finnova.UAService/Dockerfile .
docker build -t finnova-accounts-service:local     -f Finnova.AccountsService/Dockerfile .
docker build -t finnova-systemadmin-service:local  -f Finnova.SystemAdminService/Dockerfile .
docker build -t finnova-api-gateway:local          -f Finnova.ApiGateway/Dockerfile .
```

## 3. Make the images visible to the cluster

The manifest sets `imagePullPolicy: Never`, so the cluster uses locally present images
instead of pulling from a registry. Pick the line for your cluster:

- **Docker Desktop Kubernetes**: nothing to do — it shares the Docker image store.
- **minikube**:
  ```powershell
  minikube image load finnova-ua-service:local
  minikube image load finnova-accounts-service:local
  minikube image load finnova-systemadmin-service:local
  minikube image load finnova-api-gateway:local
  ```
- **kind**:
  ```powershell
  kind load docker-image finnova-ua-service:local
  kind load docker-image finnova-accounts-service:local
  kind load docker-image finnova-systemadmin-service:local
  kind load docker-image finnova-api-gateway:local
  ```

## 4. Deploy

```powershell
kubectl apply -f k8s/finnova.yaml
kubectl get pods -w        # wait until all 4 are Running and READY 1/1
kubectl get svc
```

## 5. Reach the gateway

**Option A — port-forward** (works on every cluster, keeps the URL at :5000 to match
the UI's current base URL):

```powershell
kubectl port-forward svc/finnova-api-gateway 5000:5000
# then:
curl http://localhost:5000/health
curl http://localhost:5000/api/ua/health
```

**Option B — NodePort** (Docker Desktop maps it to localhost):

```powershell
curl http://localhost:30500/health
```

## Troubleshooting

- **Pod `CrashLoopBackOff` or not READY** -> `kubectl logs deploy/finnova-ua-service`.
  A DB connection failure is the usual cause: check that service's key in the
  `finnova-db` Secret (`uaConnectionString` / `accountsConnectionString` /
  `systemadminConnectionString`), that SQL Server allows SQL auth + TCP 1433, and that
  the firewall permits it.
- **`ImagePullBackOff`** -> the image was not loaded into the cluster (step 3), or the
  tag does not match the manifest. With `imagePullPolicy: Never` the image must exist
  locally in the cluster's store.
- **Gateway returns 502 for `/api/ua/...`** -> the target backend pod is not READY yet,
  or the Service name does not match the address in `appsettings.json`. Confirm with
  `kubectl get svc` that the names are exactly `finnova-ua-service`,
  `finnova-accounts-service`, `finnova-systemadmin-service`.
- **Swagger** is disabled because `ASPNETCORE_ENVIRONMENT=Production`. To enable it for
  in-cluster testing, change that env value to `Development` for the service you want.

## Teardown

```powershell
kubectl delete -f k8s/finnova.yaml
```

